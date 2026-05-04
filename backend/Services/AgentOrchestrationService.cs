// ============================================================
// AgentOrchestrationService.cs — SK agent execution + SignalR updates
//
// This service receives a batch of TaskExecutionRequests and runs
// each one concurrently on a background thread. For each task it
// executes a pipeline via Semantic Kernel's agentic loop:
//
//   MedicationRefill / MedicationOrder / ReferralOrder:
//     Phase 1 — Data Retrieval (1–2 tool calls)
//     Phase 2 — Validation (pass/fail)
//     Phase 3 — Execute or Warn
//
//   LabOrder (2-step reasoning path):
//     Phase 1 — GetLabResults (rich data with values + reference ranges)
//     Phase 3 — Agent reads values, reasons about clinical significance,
//               calls ONE of: EscalateCriticalLabValues,
//               OrderAdditionalDiagnostics, or DocumentAndMonitor.
//               The model's reasoning text is captured from ChatHistory
//               and streamed to the browser as a "Reasoning" update
//               before the follow-up tool fires.
//
// SK's FunctionChoiceBehavior.Auto() handles the agentic loop internally:
// after each tool result is appended to ChatHistory, SK calls the model
// again until the model returns a response with no tool calls. The
// system prompt drives which tools fire in which order.
//
// The SignalRInvocationFilter intercepts every tool call and sends a
// Running-status SignalR update before each one fires. For LabOrder,
// it also extracts the model's TextContent reasoning from the last
// assistant message and pushes it as a MessageType="Reasoning" update.
// The service owns all terminal (Completed / Warned / Failed) sends.
//
// Registered as Singleton because it holds no per-request state —
// all per-task state lives in ExecuteSingleTaskAsync locals and the
// short-lived SignalRInvocationFilter instance.
// ============================================================

using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelHealthcare.Api.Hubs;
using SemanticKernelHealthcare.Api.Models;
using SemanticKernelHealthcare.Api.Plugins;

namespace SemanticKernelHealthcare.Api.Services;

public class AgentOrchestrationService(
    IServiceScopeFactory scopeFactory,
    IHubContext<TaskExecutionHub> hubContext,
    ILogger<AgentOrchestrationService> logger)
    : IAgentOrchestrationService
{
    // ------------------------------------------------------------
    // System prompt — three-phase pipeline instructions
    //
    // The prompt explicitly maps each task type to the tools it
    // should use at each phase. This prevents the model from
    // guessing wrong tools or skipping phases during demos.
    //
    // The "if validationFailed is set" branch drives the Warned state:
    // when a validation tool returns a non-null validationFailed, the
    // model calls CreateClinicalWarning instead of the execution tool,
    // and the filter sets WarnWasIssued so the service sends Warned.
    // ------------------------------------------------------------
    private const string SystemPrompt =
        "You are a healthcare workflow agent. Execute every task using the pipeline for its type.\n\n" +

        "PHASE 1 — DATA RETRIEVAL: Call the retrieval tool(s) for this task type:\n" +
        "  MedicationRefill / MedicationOrder: call GetPatientMedications AND GetPatientAllergies\n" +
        "  LabOrder:         call GetPastLabOrders\n" +
        "  ReferralOrder:    call GetPatientDemographics AND GetInsuranceCoverage\n" +
        "  LabResultReview:  call GetLabResults\n\n" +

        "PHASE 2 — VALIDATION:\n" +
        "  MedicationRefill: call ValidateMedicationRefill\n" +
        "  MedicationOrder:  call CheckDrugInteractions\n" +
        "  LabOrder:         call ValidateLabOrderIndication\n" +
        "  ReferralOrder:    call ValidateReferralAuthorization\n" +
        "  LabResultReview:  SKIP Phase 2 — proceed directly to Phase 3.\n\n" +

        "PHASE 3 — EXECUTE OR DECIDE:\n" +
        "  MedicationRefill / MedicationOrder / LabOrder / ReferralOrder: examine the " +
        "validation result's validationFailed field. If null → call the execution tool. " +
        "If set → call CreateClinicalWarning with the validationFailed value as the reason.\n" +
        "  Execution tools: MedicationRefill→RefillPrescription, " +
        "MedicationOrder→CreateMedicationOrder, LabOrder→SubmitLabOrder, " +
        "ReferralOrder→SubmitReferralOrder.\n\n" +

        "  LabResultReview: analyze the GetLabResults output. Read every analyte value, " +
        "reference range, and flag. Consider the full clinical picture, not just individual " +
        "values in isolation. Write your clinical reasoning about each flagged analyte before " +
        "deciding. Then call exactly ONE of these tools:\n" +
        "    EscalateCriticalLabValues — when any result has flag CRITICAL, or when the " +
        "combination of abnormal values indicates acute patient instability.\n" +
        "    OrderAdditionalDiagnostics — when values have HIGH or LOW flags (not CRITICAL) " +
        "and a specific follow-up test would clarify the clinical picture.\n" +
        "    DocumentAndMonitor — when all values are within normal limits or deviations " +
        "are clinically insignificant and no immediate action is required.\n\n" +

        "For LabResultReview tasks: write your clinical reasoning before calling the follow-up " +
        "tool. For all other task types: do not explain — just invoke tools.";

    public Task ExecuteTasksAsync(IEnumerable<TaskExecutionRequest> requests)
    {
        // Each task gets its own background thread. Task.Run is intentional
        // here — we want true parallelism, not cooperative async scheduling,
        // because each task blocks for multiple seconds across tool delays.
        foreach (var req in requests)
            _ = Task.Run(() => ExecuteSingleTaskAsync(req));

        return Task.CompletedTask;
    }

    private async Task ExecuteSingleTaskAsync(TaskExecutionRequest req)
    {
        var startedAt   = DateTime.UtcNow;
        var patientName = $"{req.PatientFirstName} {req.PatientLastName}".Trim();

        await Send(new TaskExecutionUpdate
        {
            TaskId    = req.TaskId,
            Status    = TaskExecutionStatus.Running,
            Message   = "Agent initializing multi-step pipeline…",
            StartedAt = startedAt
        });

        try
        {
            // ------------------------------------------------------------
            // Kernel setup
            //
            // Kernel.Clone() creates a shallow copy with its own plugin
            // collection and filter list so concurrent tasks don't share state.
            // We resolve Kernel from a new DI scope rather than injecting
            // it directly because the service is Singleton but Kernel may
            // have Scoped dependencies.
            // ------------------------------------------------------------
            using var scope  = scopeFactory.CreateScope();
            var kernel       = scope.ServiceProvider.GetRequiredService<Kernel>();
            var taskKernel   = kernel.Clone();
            taskKernel.ImportPluginFromObject(new HealthcarePlugin(), "Healthcare");

            // ------------------------------------------------------------
            // Invocation filter
            //
            // SignalRInvocationFilter implements IAutoFunctionInvocationFilter,
            // which SK calls before each tool the model decides to invoke.
            // The filter sends a Running SignalR update per tool call and
            // caches the final tool's result for the service to use in the
            // terminal send. It also flags if CreateClinicalWarning was called.
            // ------------------------------------------------------------
            var filter = new SignalRInvocationFilter(req.TaskId, startedAt, hubContext);
            taskKernel.AutoFunctionInvocationFilters.Add(filter);

            var chat    = taskKernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory(SystemPrompt);
            history.AddUserMessage(
                $"Task type: {req.Type}\nPatient: {patientName}\nDescription: {req.Description}");

            // ------------------------------------------------------------
            // FunctionChoiceBehavior.Auto — agentic multi-step loop
            //
            // Auto() enables SK's internal agentic loop: after each tool
            // result is appended to ChatHistory, the model is called again.
            // The loop continues until the model returns a response with no
            // tool calls. The system prompt controls how many tools fire
            // and in what order — no manual loop code is needed here.
            //
            // MaxTokens is set to 2000 (up from 500) because the chat history
            // grows with each appended tool result across the multi-step pipeline.
            // This caps the model's output tokens per turn, not the input.
            //
            // SKEXP0001: FunctionChoiceBehavior is experimental in the current
            // SK release. Suppressed locally to avoid polluting the whole file.
            // ------------------------------------------------------------
#pragma warning disable SKEXP0001
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature            = 0.0,
                MaxTokens              = 2000
            };
#pragma warning restore SKEXP0001

            var responses = await chat.GetChatMessageContentsAsync(history, settings, taskKernel);

            // ------------------------------------------------------------
            // Token usage extraction
            //
            // Token counts live in the response metadata under "Usage".
            // We use reflection to read them because the type isn't directly
            // accessible via the public SK API. Failures are silently swallowed
            // because token counts are optional display info.
            // ------------------------------------------------------------
            int? promptTokens = null, completionTokens = null;
            var lastMsg = responses.LastOrDefault();
            try
            {
                if (lastMsg?.Metadata?.TryGetValue("Usage", out var usageObj) == true && usageObj != null)
                {
                    var t            = usageObj.GetType();
                    promptTokens     = (int?)t.GetProperty("InputTokenCount")?.GetValue(usageObj);
                    completionTokens = (int?)t.GetProperty("OutputTokenCount")?.GetValue(usageObj);
                }
            }
            catch { /* token info is optional */ }

            // ------------------------------------------------------------
            // Single terminal send
            //
            // The filter sends only Running messages. The service always
            // owns the one Completed / Warned / Failed terminal message,
            // so token counts are always included and there is no two-phase
            // merge needed on the frontend.
            // ------------------------------------------------------------
            if (!filter.ToolWasCalled)
            {
                // Shouldn't happen given the system prompt, but defensive fallback.
                await Send(new TaskExecutionUpdate
                {
                    TaskId           = req.TaskId,
                    Status           = TaskExecutionStatus.Completed,
                    Message          = "Task completed.",
                    PromptTokens     = promptTokens,
                    CompletionTokens = completionTokens,
                    StartedAt        = startedAt,
                    CompletedAt      = DateTime.UtcNow,
                    TotalSteps       = filter.StepCount
                });
            }
            else if (filter.WarnWasIssued)
            {
                // Validation failed — the agent called CreateClinicalWarning.
                // Details contains the warning JSON (warningId, reason, severity, requiresReview).
                await Send(new TaskExecutionUpdate
                {
                    TaskId           = req.TaskId,
                    Status           = TaskExecutionStatus.Warned,
                    Message          = "Clinical warning issued — action could not be completed safely.",
                    ToolName         = filter.LastToolName,
                    Details          = filter.LastDetails,
                    PromptTokens     = promptTokens,
                    CompletionTokens = completionTokens,
                    StartedAt        = startedAt,
                    CompletedAt      = filter.LastCompletedAt ?? DateTime.UtcNow,
                    TotalSteps       = filter.StepCount
                });
            }
            else
            {
                // Validation passed — the agent called the execution tool.
                // Details contains the execution result (e.g., pharmacy confirmation JSON).
                await Send(new TaskExecutionUpdate
                {
                    TaskId           = req.TaskId,
                    Status           = TaskExecutionStatus.Completed,
                    Message          = $"{filter.LastToolName} completed successfully.",
                    ToolName         = filter.LastToolName,
                    Details          = filter.LastDetails,
                    PromptTokens     = promptTokens,
                    CompletionTokens = completionTokens,
                    StartedAt        = startedAt,
                    CompletedAt      = filter.LastCompletedAt ?? DateTime.UtcNow,
                    TotalSteps       = filter.StepCount
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Task {TaskId} failed", req.TaskId);
            await Send(new TaskExecutionUpdate
            {
                TaskId      = req.TaskId,
                Status      = TaskExecutionStatus.Failed,
                Message     = ex.Message,
                StartedAt   = startedAt,
                CompletedAt = DateTime.UtcNow
            });
        }
    }

    private Task Send(TaskExecutionUpdate update) =>
        hubContext.Clients.All.SendAsync("TaskUpdated", update);
}

// ============================================================
// SignalRInvocationFilter — IAutoFunctionInvocationFilter impl
//
// SK calls OnAutoFunctionInvocationAsync around every tool call
// the model makes during the agentic loop. This filter uses that
// hook to push a Running SignalR update before each tool fires,
// giving the browser a step-by-step view of the agent's progress.
//
// The filter does NOT send Completed messages — only the service
// does, once, after the full pipeline finishes (with token counts).
//
// Cached values (LastToolName, LastDetails, LastCompletedAt) let
// the service include the final tool's result in the terminal send
// without needing a second SignalR round-trip.
// ============================================================
internal sealed class SignalRInvocationFilter(
    string taskId,
    DateTime startedAt,
    IHubContext<TaskExecutionHub> hubContext)
    : IAutoFunctionInvocationFilter
{
    private int _stepNumber = 0;
    private string? _lastReasoningEmitted;

    // True once any tool has been called. Used by the service to
    // detect the unexpected case where the model made no tool calls.
    public bool ToolWasCalled { get; private set; }

    // True if the last tool called was CreateClinicalWarning.
    // Signals the service to send Warned instead of Completed.
    public bool WarnWasIssued { get; private set; }

    // The actual number of tool calls made across the full pipeline.
    // MedicationRefill/MedicationOrder = 4 steps; LabOrder/ReferralOrder = 3 steps.
    // Used by the service for TotalSteps on the terminal message.
    public int StepCount => _stepNumber;

    // Cached from the last tool invocation, included in the terminal send.
    public string? LastToolName { get; private set; }
    public string? LastDetails { get; private set; }
    public DateTime? LastCompletedAt { get; private set; }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        ToolWasCalled = true;
        _stepNumber++;
        var toolName = context.Function.Name;

        // Extract any reasoning text the model wrote before deciding to call this tool.
        // SK appends the model's full response (TextContent + FunctionCallContent items)
        // to ChatHistory before firing the filter. We pull out just the text portion so
        // the browser can display the model's chain-of-thought between tool-call entries.
        // The dedup guard (_lastReasoningEmitted) prevents double-sends when the model
        // emits multiple tool calls in a single response.
        var lastAssistant = context.ChatHistory.LastOrDefault(m => m.Role == AuthorRole.Assistant);
        if (lastAssistant?.Items != null)
        {
            var reasoningText = string.Concat(
                lastAssistant.Items
                    .OfType<TextContent>()
                    .Select(tc => tc.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (!string.IsNullOrEmpty(reasoningText) && reasoningText != _lastReasoningEmitted)
            {
                _lastReasoningEmitted = reasoningText;
                await hubContext.Clients.All.SendAsync("TaskUpdated", new TaskExecutionUpdate
                {
                    TaskId      = taskId,
                    Status      = TaskExecutionStatus.Running,
                    Message     = reasoningText,
                    MessageType = "Reasoning",
                    StepNumber  = _stepNumber,
                    StartedAt   = startedAt
                });
            }
        }

        // Notify the browser which tool is about to run and which step we're on.
        await hubContext.Clients.All.SendAsync("TaskUpdated", new TaskExecutionUpdate
        {
            TaskId     = taskId,
            Status     = TaskExecutionStatus.Running,
            ToolName   = toolName,
            Message    = $"Step {_stepNumber}: Calling {toolName}…",
            StepNumber = _stepNumber,
            StartedAt  = startedAt
        });

        // Execute the actual KernelFunction (e.g. ValidateMedicationRefill).
        await next(context);

        // Cache result for the service's terminal send.
        LastToolName    = toolName;
        LastDetails     = context.Result?.GetValue<string>() ?? "{}";
        LastCompletedAt = DateTime.UtcNow;

        // Track whether the pipeline ended in a warning rather than an execution.
        if (toolName == "CreateClinicalWarning")
            WarnWasIssued = true;
    }
}

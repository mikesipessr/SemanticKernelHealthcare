// ============================================================
// AgentOrchestrationService.cs — SK agent execution + SignalR updates
//
// This service receives a batch of TaskExecutionRequests and runs
// each one concurrently on a background thread. For each task it:
//
//   1. Clones the shared Kernel and imports HealthcarePlugin so
//      the agent has access to the four healthcare tools.
//   2. Attaches a SignalRInvocationFilter that intercepts each tool
//      call and pushes real-time status updates to connected browsers.
//   3. Sends a chat message to GPT-4o with FunctionChoiceBehavior.Auto,
//      letting the model decide which tool matches the task.
//   4. After the LLM call completes, patches token usage onto the
//      final Completed message already sent by the filter.
//
// Registered as a Singleton in Program.cs because it holds no
// per-request state — all per-task state lives in ExecuteSingleTaskAsync
// local variables and the short-lived SignalRInvocationFilter instance.
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
    // The system prompt keeps the agent focused and predictable.
    // "Use exactly one tool" prevents the model from chaining multiple
    // calls or writing an explanation instead of invoking a function.
    // "Do not explain" suppresses the conversational response the model
    // sometimes adds after a successful tool call — we don't need prose,
    // just the tool invocation.
    private const string SystemPrompt =
        "You are a healthcare workflow agent. You will be given a task description for a patient. " +
        "Use exactly one of the available tools to complete the task based on its type. " +
        "After calling the tool, confirm the action was completed. Do not explain — just invoke the tool.";

    public Task ExecuteTasksAsync(IEnumerable<TaskExecutionRequest> requests)
    {
        // Each task gets its own background thread. Task.Run is intentional
        // here — we want true parallelism, not cooperative async scheduling,
        // because each task blocks for ~1.5s on the simulated tool delay.
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
            Message   = "Agent is analyzing the task...",
            StartedAt = startedAt
        });

        try
        {
            // ------------------------------------------------------------
            // Kernel setup
            //
            // We resolve the Kernel from a new DI scope rather than
            // injecting it directly. The Kernel itself is registered as
            // Singleton, but IServiceScopeFactory lets us create a short-
            // lived scope that correctly resolves any Scoped dependencies
            // the Kernel or its services might have.
            //
            // Kernel.Clone() creates a shallow copy with its own plugin
            // collection and filter list, so tasks can't interfere with
            // each other's plugin state when running concurrently.
            // ------------------------------------------------------------
            using var scope      = scopeFactory.CreateScope();
            var kernel           = scope.ServiceProvider.GetRequiredService<Kernel>();
            var taskKernel       = kernel.Clone();
            taskKernel.ImportPluginFromObject(new HealthcarePlugin(), "Healthcare");

            // ------------------------------------------------------------
            // Invocation filter
            //
            // SignalRInvocationFilter implements IAutoFunctionInvocationFilter,
            // which SK calls before and after every tool invocation the model
            // decides to make. We use it to push real-time "Calling X..." and
            // "X completed" SignalR messages without polling.
            //
            // The filter also caches the tool name, result, and completion
            // timestamp so the service can re-send a final Completed message
            // with token counts attached (see two-phase completion below).
            // ------------------------------------------------------------
            var filter = new SignalRInvocationFilter(req.TaskId, startedAt, hubContext);
            taskKernel.AutoFunctionInvocationFilters.Add(filter);

            var chat    = taskKernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory(SystemPrompt);
            history.AddUserMessage(
                $"Task type: {req.Type}\nPatient: {patientName}\nDescription: {req.Description}");

            // ------------------------------------------------------------
            // FunctionChoiceBehavior.Auto — agentic tool selection
            //
            // With Auto(), the model decides whether to call a tool and
            // which one, based on the task description and the tool schemas
            // from HealthcarePlugin. Temperature = 0 keeps selection
            // deterministic — there's no creativity needed here.
            //
            // SKEXP0001 suppression: FunctionChoiceBehavior is still marked
            // experimental in the current SK release. We suppress the warning
            // locally rather than globally because the rest of the codebase
            // doesn't use experimental APIs.
            // ------------------------------------------------------------
#pragma warning disable SKEXP0001
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature            = 0.0,
                MaxTokens              = 500
            };
#pragma warning restore SKEXP0001

            var responses = await chat.GetChatMessageContentsAsync(history, settings, taskKernel);

            // ------------------------------------------------------------
            // Token usage extraction
            //
            // The SDK doesn't expose token counts via a typed property;
            // they're buried in the response metadata dictionary under
            // the key "Usage". We use reflection to read InputTokenCount
            // and OutputTokenCount from whatever object the connector puts
            // there. This is brittle against SDK changes, but token counts
            // are optional display info — failures are silently swallowed.
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
            // Two-phase completion
            //
            // When a tool is called, SignalRInvocationFilter sends its own
            // Completed message that carries ToolName and Details. If we
            // then sent a second Completed message here, it would overwrite
            // those fields on the frontend with nulls.
            //
            // Instead:
            //   - If no tool ran (ToolWasCalled = false), we send a generic
            //     Completed. This shouldn't happen given the system prompt,
            //     but it's a safe fallback.
            //   - If a tool ran (ToolWasCalled = true), we re-send the
            //     filter's cached data plus the token counts. The frontend
            //     merges the two Completed messages rather than replacing,
            //     so ToolName and Details are preserved.
            // ------------------------------------------------------------
            if (!filter.ToolWasCalled)
            {
                await Send(new TaskExecutionUpdate
                {
                    TaskId           = req.TaskId,
                    Status           = TaskExecutionStatus.Completed,
                    Message          = "Task completed.",
                    PromptTokens     = promptTokens,
                    CompletionTokens = completionTokens,
                    StartedAt        = startedAt,
                    CompletedAt      = DateTime.UtcNow
                });
            }
            else
            {
                await Send(new TaskExecutionUpdate
                {
                    TaskId           = req.TaskId,
                    Status           = TaskExecutionStatus.Completed,
                    Message          = filter.LastCompletionMessage,
                    ToolName         = filter.LastToolName,
                    Details          = filter.LastDetails,
                    PromptTokens     = promptTokens,
                    CompletionTokens = completionTokens,
                    StartedAt        = startedAt,
                    CompletedAt      = filter.LastCompletedAt ?? DateTime.UtcNow
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
// the model decides to make. This filter uses that hook to push
// two SignalR messages per tool call:
//
//   Before next(context): "Calling <ToolName>..." (status = Running)
//   After  next(context): "<ToolName> completed." (status = Completed)
//                          + the raw JSON result in Details
//
// The filter also caches the last tool's name, result, and timestamp
// so AgentOrchestrationService can re-send a final Completed message
// with token counts patched in (token counts aren't available until
// after GetChatMessageContentsAsync returns, which is after the filter).
// ============================================================
internal sealed class SignalRInvocationFilter(
    string taskId,
    DateTime startedAt,
    IHubContext<TaskExecutionHub> hubContext)
    : IAutoFunctionInvocationFilter
{
    // True once the filter has fired at least once for this task.
    // Used by AgentOrchestrationService to decide whether to send
    // a generic fallback Completed or to patch token counts onto
    // the filter's already-sent Completed message.
    public bool ToolWasCalled { get; private set; }

    // Cached values from the last tool invocation, re-sent with
    // token counts by AgentOrchestrationService after LLM call returns.
    public string? LastToolName { get; private set; }
    public string? LastDetails { get; private set; }
    public string LastCompletionMessage { get; private set; } = "";
    public DateTime? LastCompletedAt { get; private set; }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        ToolWasCalled = true;
        var toolName  = context.Function.Name;

        // Notify the browser the agent has chosen a tool and is calling it.
        await hubContext.Clients.All.SendAsync("TaskUpdated", new TaskExecutionUpdate
        {
            TaskId    = taskId,
            Status    = TaskExecutionStatus.Running,
            ToolName  = toolName,
            Message   = $"Calling {toolName}...",
            StartedAt = startedAt
        });

        // Execute the actual KernelFunction (e.g. RefillPrescriptionAsync).
        await next(context);

        var details     = context.Result?.GetValue<string>() ?? "{}";
        var completedAt = DateTime.UtcNow;
        var message     = $"{toolName} completed successfully.";

        // Cache for AgentOrchestrationService to re-send with token counts.
        LastToolName          = toolName;
        LastDetails           = details;
        LastCompletionMessage = message;
        LastCompletedAt       = completedAt;

        // Push the initial Completed message. It has ToolName and Details
        // but no token counts yet — those arrive after GetChatMessageContentsAsync
        // returns and will be merged in by a second push from the service.
        await hubContext.Clients.All.SendAsync("TaskUpdated", new TaskExecutionUpdate
        {
            TaskId      = taskId,
            Status      = TaskExecutionStatus.Completed,
            ToolName    = toolName,
            Message     = message,
            Details     = details,
            StartedAt   = startedAt,
            CompletedAt = completedAt
        });
    }
}

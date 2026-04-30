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
    private const string SystemPrompt =
        "You are a healthcare workflow agent. You will be given a task description for a patient. " +
        "Use exactly one of the available tools to complete the task based on its type. " +
        "After calling the tool, confirm the action was completed. Do not explain — just invoke the tool.";

    public Task ExecuteTasksAsync(IEnumerable<TaskExecutionRequest> requests)
    {
        foreach (var req in requests)
            _ = Task.Run(() => ExecuteSingleTaskAsync(req));

        return Task.CompletedTask;
    }

    private async Task ExecuteSingleTaskAsync(TaskExecutionRequest req)
    {
        var startedAt = DateTime.UtcNow;
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
            using var scope = scopeFactory.CreateScope();
            var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();

            var taskKernel = kernel.Clone();
            taskKernel.ImportPluginFromObject(new HealthcarePlugin(), "Healthcare");

            var filter = new SignalRInvocationFilter(req.TaskId, startedAt, hubContext);
            taskKernel.AutoFunctionInvocationFilters.Add(filter);

            var chat = taskKernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory(SystemPrompt);
            history.AddUserMessage(
                $"Task type: {req.Type}\nPatient: {patientName}\nDescription: {req.Description}");

#pragma warning disable SKEXP0001
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature            = 0.0,
                MaxTokens              = 500
            };
#pragma warning restore SKEXP0001

            var responses = await chat.GetChatMessageContentsAsync(history, settings, taskKernel);

            // Extract token usage from the last response's metadata.
            int? promptTokens = null, completionTokens = null;
            var lastMsg = responses.LastOrDefault();
            try
            {
                if (lastMsg?.Metadata?.TryGetValue("Usage", out var usageObj) == true && usageObj != null)
                {
                    var t = usageObj.GetType();
                    promptTokens     = (int?)t.GetProperty("InputTokenCount")?.GetValue(usageObj);
                    completionTokens = (int?)t.GetProperty("OutputTokenCount")?.GetValue(usageObj);
                }
            }
            catch { /* token info is optional */ }

            // Only send a final Completed if the filter never fired (no tool was called).
            // When a tool was called, the filter's Completed message is the terminal state —
            // sending another one here would overwrite the details/toolName on the frontend.
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
                // Patch the token counts onto the already-sent Completed message via a summary push.
                // The frontend merges this into the existing Completed state without overwriting details.
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

internal sealed class SignalRInvocationFilter(
    string taskId,
    DateTime startedAt,
    IHubContext<TaskExecutionHub> hubContext)
    : IAutoFunctionInvocationFilter
{
    public bool ToolWasCalled { get; private set; }
    public string? LastToolName { get; private set; }
    public string? LastDetails { get; private set; }
    public string LastCompletionMessage { get; private set; } = "";
    public DateTime? LastCompletedAt { get; private set; }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        ToolWasCalled = true;
        var toolName = context.Function.Name;

        await hubContext.Clients.All.SendAsync("TaskUpdated", new TaskExecutionUpdate
        {
            TaskId    = taskId,
            Status    = TaskExecutionStatus.Running,
            ToolName  = toolName,
            Message   = $"Calling {toolName}...",
            StartedAt = startedAt
        });

        await next(context);

        var details   = context.Result?.GetValue<string>() ?? "{}";
        var completedAt = DateTime.UtcNow;
        var message   = $"{toolName} completed successfully.";

        // Cache for the service to re-send with token counts attached.
        LastToolName          = toolName;
        LastDetails           = details;
        LastCompletionMessage = message;
        LastCompletedAt       = completedAt;

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

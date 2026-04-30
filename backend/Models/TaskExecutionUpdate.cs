namespace SemanticKernelHealthcare.Api.Models;

public class TaskExecutionUpdate
{
    public string TaskId { get; set; } = "";
    public TaskExecutionStatus Status { get; set; }
    public string? ToolName { get; set; }
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

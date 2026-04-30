// ============================================================
// TaskExecutionUpdate.cs — SignalR push payload
//
// This is the message shape pushed to the browser via SignalR
// each time a task's state changes. The client receives a stream
// of these over the lifetime of a task: typically one Running
// message when the agent starts, another when it begins calling
// a tool, and a final Completed or Failed.
//
// Several fields are nullable because they are only populated at
// specific lifecycle stages:
//
//   ToolName    — set once the model has selected a tool; null
//                 while the agent is still "thinking."
//
//   Details     — the raw JSON result from the tool; only present
//                 on the Completed update after the tool returns.
//
//   PromptTokens / CompletionTokens — populated on the final
//                 Completed push after the LLM response is parsed.
//                 They arrive in a second Completed message that
//                 the frontend merges with the first (which carries
//                 ToolName and Details). See AgentOrchestrationService
//                 for the two-phase completion logic.
//
//   CompletedAt — null on Running updates; set when the task finishes.
// ============================================================

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

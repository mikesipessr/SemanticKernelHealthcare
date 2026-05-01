// ============================================================
// TaskExecutionUpdate.cs — SignalR push payload
//
// This is the message shape pushed to the browser via SignalR
// each time a task's state changes. The client receives a stream
// of these over the lifetime of a task.
//
// For the multi-step pipeline, a typical task produces:
//   - 1 initial Running message ("Agent initializing...")
//   - N Running messages, one before each tool call (with StepNumber)
//   - 1 terminal Completed, Warned, or Failed message
//
// Several fields are nullable because they are only populated at
// specific lifecycle stages:
//
//   ToolName    — set once the agent has selected a tool to call.
//
//   Details     — raw JSON result from the final tool in the chain.
//                 On Completed: the execution tool's output.
//                 On Warned: the CreateClinicalWarning tool's output.
//
//   StepNumber  — 1-based counter incremented by the filter before
//                 each tool call. Present on Running messages only.
//
//   TotalSteps  — actual number of tool calls made across the full
//                 pipeline. Set once on the terminal message.
//                 Varies by task type (LabOrder = 3, MedicationRefill = 4).
//
//   PromptTokens / CompletionTokens — populated on the single
//                 terminal message after the LLM pipeline completes.
//
//   CompletedAt — null on Running updates; set on terminal messages.
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
    public int? StepNumber { get; set; }
    public int? TotalSteps { get; set; }
}

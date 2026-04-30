// ============================================================
// TasksController.cs — Task execution endpoint
//
// Receives a batch of task execution requests and hands them off
// to AgentOrchestrationService, then immediately returns 202 Accepted.
//
// The 202 response is intentional: each task runs an LLM call plus
// a simulated 1.5-second tool delay, so waiting for all tasks to
// finish before responding would block the HTTP connection for several
// seconds. Instead, the controller fires the work off in the background
// and the client gets real-time progress via SignalR.
// ============================================================

using Microsoft.AspNetCore.Mvc;
using SemanticKernelHealthcare.Api.Models;
using SemanticKernelHealthcare.Api.Services;

namespace SemanticKernelHealthcare.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController(IAgentOrchestrationService orchestration) : ControllerBase
{
    [HttpPost("execute")]
    public IActionResult Execute([FromBody] List<TaskExecutionRequest> requests)
    {
        if (requests is null || requests.Count == 0)
            return BadRequest("No tasks provided.");

        // Fire-and-forget: ExecuteTasksAsync kicks off background Task.Run
        // calls internally and returns a completed Task immediately. The
        // discard (_) suppresses the CS4014 "not awaited" compiler warning.
        // Results are delivered to the client via SignalR, not this response.
        _ = orchestration.ExecuteTasksAsync(requests);

        return Accepted();
    }
}

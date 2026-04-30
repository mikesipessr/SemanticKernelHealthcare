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

        _ = orchestration.ExecuteTasksAsync(requests);

        return Accepted();
    }
}

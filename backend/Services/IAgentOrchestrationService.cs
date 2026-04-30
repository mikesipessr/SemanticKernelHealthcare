using SemanticKernelHealthcare.Api.Models;

namespace SemanticKernelHealthcare.Api.Services;

public interface IAgentOrchestrationService
{
    Task ExecuteTasksAsync(IEnumerable<TaskExecutionRequest> requests);
}

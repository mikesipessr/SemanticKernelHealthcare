using Microsoft.AspNetCore.SignalR;

namespace SemanticKernelHealthcare.Api.Hubs;

public class TaskExecutionHub : Hub
{
    // Server-push only. Messages sent via IHubContext<TaskExecutionHub>.
}

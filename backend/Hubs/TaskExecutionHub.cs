// ============================================================
// TaskExecutionHub.cs — SignalR hub for task execution updates
//
// This hub is intentionally empty. All outbound messages are sent
// server-to-client via IHubContext<TaskExecutionHub>, injected into
// AgentOrchestrationService. The hub class itself just needs to exist
// so ASP.NET Core knows the endpoint type when MapHub is called in
// Program.cs.
//
// Clients connect to /hubs/tasks and listen for "TaskUpdated" events.
// They never push messages back — this is a server-push-only channel.
// ============================================================

using Microsoft.AspNetCore.SignalR;

namespace SemanticKernelHealthcare.Api.Hubs;

public class TaskExecutionHub : Hub
{
}

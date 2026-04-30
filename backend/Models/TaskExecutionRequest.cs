namespace SemanticKernelHealthcare.Api.Models;

public class TaskExecutionRequest
{
    public string TaskId { get; set; } = "";
    public TaskType Type { get; set; }
    public string PatientFirstName { get; set; } = "";
    public string PatientLastName { get; set; } = "";
    public string Description { get; set; } = "";
}

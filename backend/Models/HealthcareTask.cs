namespace SemanticKernelHealthcare.Api.Models;

public class HealthcareTask
{
    public TaskType Type { get; set; }
    public string PatientFirstName { get; set; } = "";
    public string PatientLastName { get; set; } = "";
    public string Description { get; set; } = "";
}

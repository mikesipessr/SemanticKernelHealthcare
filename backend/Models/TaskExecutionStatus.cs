namespace SemanticKernelHealthcare.Api.Models;

public enum TaskExecutionStatus
{
    Running,
    Completed,
    Warned,   // agent completed analysis but determined the action should not proceed
    Failed
}

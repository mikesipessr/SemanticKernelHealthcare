// ============================================================
// HealthcareTask.cs — A single structured task extracted from a
//                     clinical note by the AI classifier.
//
// This model is intentionally minimal for version 1. It contains
// only what is needed to display the task in the UI and to route
// it to the correct tool in the upcoming agentic layer.
//
// Design note: All string properties default to "" rather than null.
// This avoids null checks throughout the application and ensures the
// JSON serializer always emits the field (even if empty), which
// keeps the API contract consistent for frontend consumers.
// ============================================================

namespace SemanticKernelHealthcare.Api.Models;

public class HealthcareTask
{
    /// <summary>
    /// The category of this task. Used by the future agentic layer
    /// to select the appropriate tool for processing.
    /// </summary>
    public TaskType Type { get; set; }

    /// <summary>Patient's first name as mentioned in the clinical note.</summary>
    public string PatientFirstName { get; set; } = "";

    /// <summary>Patient's last name as mentioned in the clinical note.</summary>
    public string PatientLastName { get; set; } = "";

    /// <summary>
    /// A plain-English description of what action needs to be taken.
    /// For example: "Schedule a referral to oncology for Jane Doe."
    /// This becomes the instruction passed to the appropriate tool
    /// in the agentic processing layer.
    /// </summary>
    public string Description { get; set; } = "";
}

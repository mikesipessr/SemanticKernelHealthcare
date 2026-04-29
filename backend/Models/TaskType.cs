// ============================================================
// TaskType.cs — Discriminator enum for healthcare task categories
//
// This enum defines the four task types the AI classifier is trained
// to recognize. Adding a new type here is the first step to supporting
// it — the system prompt in TaskClassificationService and the frontend
// TaskCard component also need to be updated to match.
//
// In the future agentic layer (planned for the next blog post), each
// TaskType will map to a specific Semantic Kernel tool:
//   MedicationRefill  → call the pharmacy system API
//   MedicationOrder   → submit to the EHR medication ordering module
//   ReferralOrder     → send a referral fax or HL7 message
//   LabOrder          → create a lab requisition
// ============================================================

namespace SemanticKernelHealthcare.Api.Models;

public enum TaskType
{
    /// <summary>Request to refill an existing prescription.</summary>
    MedicationRefill,

    /// <summary>Request for a new medication to be prescribed.</summary>
    MedicationOrder,

    /// <summary>Request to refer the patient to a specialist.</summary>
    ReferralOrder,

    /// <summary>Request for laboratory tests to be ordered.</summary>
    LabOrder
}

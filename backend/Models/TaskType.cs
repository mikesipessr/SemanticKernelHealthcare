// ============================================================
// TaskType.cs — Discriminator enum for healthcare task categories
//
// This enum defines the task types the AI classifier is trained
// to recognize. Adding a new type here is the first step to supporting
// it — the system prompt in TaskClassificationService and the frontend
// TaskCard component also need to be updated to match.
//
// Each TaskType maps to a distinct agent pipeline:
//   MedicationRefill  → retrieve meds+allergies → validate → refill
//   MedicationOrder   → retrieve meds+allergies → drug-interaction check → order
//   ReferralOrder     → retrieve demographics+insurance → authorize → submit referral
//   LabOrder          → validate indication → submit lab requisition
//   LabResultReview   → retrieve results → reason about values → escalate/workup/document
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

    /// <summary>Request for new laboratory tests to be ordered.</summary>
    LabOrder,

    /// <summary>
    /// Review of lab results that have already come back. The agent retrieves
    /// the results, reasons about clinical significance, and selects the
    /// appropriate follow-up action (escalate, order more tests, or document).
    /// </summary>
    LabResultReview
}

// ============================================================
// HealthcarePlugin.cs — Semantic Kernel tool definitions
//
// This class is a Semantic Kernel "plugin" — a collection of
// methods the AI agent can choose to call when completing tasks.
// SK discovers these methods via the [KernelFunction] attribute
// and presents them to the model as tool definitions (the same
// mechanism as OpenAI function calling).
//
// The [Description] attributes are not just documentation — they
// are sent to the model as part of the tool schema. Clear, specific
// descriptions are what let the model reliably pick the right tool
// for each task type and phase.
//
// Tools are organized into four groups:
//
//   Phase 1 — Data Retrieval (5 tools):
//     GetPatientMedications, GetPatientAllergies,
//     GetPastLabOrders, GetPatientDemographics, GetInsuranceCoverage
//
//   Phase 2 — Validation (4 tools):
//     ValidateMedicationRefill, CheckDrugInteractions,
//     ValidateLabOrderIndication, ValidateReferralAuthorization
//
//     Validation tools randomly fail ~25% of the time (for demo
//     purposes), returning a non-null `validationFailed` field with
//     a plausible clinical reason. The agent reads this and calls
//     CreateClinicalWarning instead of the execution tool.
//
//   Warning (1 tool):
//     CreateClinicalWarning — called by the agent when validation fails
//
//   Phase 3 — Execution (4 tools):
//     RefillPrescription, CreateMedicationOrder,
//     SubmitLabOrder, SubmitReferralOrder
//
// All functions are demo stubs: they delay 850ms (retrieval/validation)
// or 1500ms (execution) and return realistic-looking JSON. Swapping in
// real implementations means replacing the Task.Delay and fake-data
// logic — the SK plumbing stays the same.
// ============================================================

using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace SemanticKernelHealthcare.Api.Plugins;

public class HealthcarePlugin
{
    private static readonly Random _rng = new();

    // ── Phase 1: Data Retrieval ──────────────────────────────

    [KernelFunction("GetPatientMedications")]
    [Description("Retrieves the current active medication list for a patient from the EHR.")]
    public async Task<string> GetPatientMedicationsAsync(
        [Description("Full name of the patient")] string patientName)
    {
        await Task.Delay(850);
        var result = new
        {
            patientName,
            medications = new[]
            {
                new
                {
                    name       = Pick("Lisinopril", "Metformin", "Atorvastatin", "Amlodipine", "Losartan"),
                    dose       = Pick("10mg", "500mg", "20mg", "5mg", "50mg"),
                    frequency  = "once daily",
                    lastFilled = DateTime.UtcNow.AddDays(-_rng.Next(10, 60)).ToString("yyyy-MM-dd")
                },
                new
                {
                    name       = Pick("Omeprazole", "Metoprolol", "Sertraline", "Levothyroxine", "Aspirin"),
                    dose       = Pick("20mg", "25mg", "50mg", "100mcg", "81mg"),
                    frequency  = "once daily",
                    lastFilled = DateTime.UtcNow.AddDays(-_rng.Next(10, 60)).ToString("yyyy-MM-dd")
                }
            },
            retrievedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("GetPatientAllergies")]
    [Description("Retrieves documented drug allergies and adverse reactions for a patient from the EHR.")]
    public async Task<string> GetPatientAllergiesAsync(
        [Description("Full name of the patient")] string patientName)
    {
        await Task.Delay(850);
        // ~33% of patients have a documented allergy for demo variety
        var hasAllergy = _rng.Next(3) == 0;
        var result = new
        {
            patientName,
            allergies = hasAllergy
                ? new[]
                  {
                      new
                      {
                          allergen  = Pick("Penicillin", "Sulfa drugs", "NSAIDs", "Codeine", "Cephalosporins"),
                          reaction  = Pick("Anaphylaxis", "Rash", "Angioedema", "GI upset", "Urticaria"),
                          severity  = Pick("High", "Moderate")
                      }
                  }
                : Array.Empty<object>(),
            retrievedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("GetPastLabOrders")]
    [Description("Retrieves recent laboratory order history for a patient to check for duplicate or recent tests.")]
    public async Task<string> GetPastLabOrdersAsync(
        [Description("Full name of the patient")] string patientName)
    {
        await Task.Delay(850);
        var result = new
        {
            patientName,
            recentOrders = new[]
            {
                new
                {
                    orderId     = $"LAB-{_rng.Next(10_000, 99_999)}",
                    tests       = Pick("CBC, BMP", "HbA1c", "Lipid Panel", "TSH, Free T4", "Comprehensive Metabolic Panel"),
                    orderedDate = DateTime.UtcNow.AddDays(-_rng.Next(30, 180)).ToString("yyyy-MM-dd"),
                    status      = "Resulted"
                }
            },
            retrievedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("GetPatientDemographics")]
    [Description("Retrieves patient demographic information including age, address, and primary care provider from the EHR.")]
    public async Task<string> GetPatientDemographicsAsync(
        [Description("Full name of the patient")] string patientName)
    {
        await Task.Delay(850);
        var result = new
        {
            patientName,
            dateOfBirth          = DateTime.UtcNow.AddYears(-_rng.Next(30, 80)).ToString("yyyy-MM-dd"),
            gender               = Pick("Male", "Female"),
            address              = $"{_rng.Next(100, 9999)} {Pick("Oak", "Maple", "Pine", "Elm", "Cedar")} St",
            phone                = $"({_rng.Next(200, 999)}) 555-{_rng.Next(1000, 9999)}",
            primaryCareProvider  = $"Dr. {Pick("Williams", "Johnson", "Smith", "Brown", "Garcia")}",
            retrievedAt          = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("GetInsuranceCoverage")]
    [Description("Retrieves current insurance plan details and specialist referral coverage for a patient.")]
    public async Task<string> GetInsuranceCoverageAsync(
        [Description("Full name of the patient")] string patientName)
    {
        await Task.Delay(850);
        var result = new
        {
            patientName,
            insurancePlan        = Pick("Blue Cross PPO", "Aetna HMO", "UnitedHealth EPO", "Cigna POS", "Humana HMO"),
            memberId             = $"MEM-{_rng.Next(100_000, 999_999)}",
            referralRequiresAuth = _rng.Next(2) == 0,
            specialistCopay      = $"${Pick("30", "40", "50", "60", "75")}",
            deductibleMet        = _rng.Next(2) == 0,
            retrievedAt          = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    // ── Phase 2: Validation ──────────────────────────────────
    //
    // Each validation tool returns { validationPassed, validationFailed }.
    // When validationFailed is non-null, the agent calls CreateClinicalWarning
    // with that reason instead of proceeding to the execution tool.
    // The ~25% failure rate ensures the Warned card state appears in demos.

    [KernelFunction("ValidateMedicationRefill")]
    [Description("Validates whether a prescription refill request meets clinical guidelines for refill frequency, quantity limits, and controlled substance rules.")]
    public async Task<string> ValidateMedicationRefillAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Description of the medication refill")] string description)
    {
        await Task.Delay(850);
        var failed = _rng.Next(4) == 0; // ~25%
        var result = new
        {
            patientName,
            validationPassed = !failed,
            validationFailed = failed
                ? Pick(
                    "Refill requested too soon — last fill was 12 days ago, minimum interval is 25 days.",
                    "Quantity limit exceeded — plan allows 30-day supply, 90-day supply was requested.",
                    "Controlled substance refill requires prescriber confirmation before dispensing.")
                : (string?)null,
            checkedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("CheckDrugInteractions")]
    [Description("Checks a proposed new medication against the patient's current medications and documented allergies for dangerous interactions or duplicate therapy.")]
    public async Task<string> CheckDrugInteractionsAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("The proposed new medication to check")] string proposedMedication)
    {
        await Task.Delay(850);
        var failed = _rng.Next(4) == 0; // ~25%
        var result = new
        {
            patientName,
            proposedMedication,
            validationPassed = !failed,
            validationFailed = failed
                ? Pick(
                    "Potential interaction detected: proposed medication may increase bleeding risk when combined with current anticoagulant therapy.",
                    "Allergy conflict: patient has documented hypersensitivity to this drug class — dispensing is contraindicated.",
                    "Duplicate therapy detected: patient is already receiving a medication from the same drug class.")
                : (string?)null,
            checkedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("ValidateLabOrderIndication")]
    [Description("Validates that a lab order is clinically indicated by checking for recent duplicate orders and guideline compliance.")]
    public async Task<string> ValidateLabOrderIndicationAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Description of the lab tests being ordered")] string description)
    {
        await Task.Delay(850);
        var failed = _rng.Next(4) == 0; // ~25%
        var result = new
        {
            patientName,
            validationPassed = !failed,
            validationFailed = failed
                ? Pick(
                    "Duplicate order detected: same test panel was resulted 18 days ago — guidelines recommend a minimum 90-day interval.",
                    "Medical necessity not established: no qualifying diagnosis code on file for this test.",
                    "Order requires prior authorization from this insurance plan before the draw can be scheduled.")
                : (string?)null,
            checkedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("ValidateReferralAuthorization")]
    [Description("Validates that a specialist referral meets insurance prior authorization requirements and confirms network availability.")]
    public async Task<string> ValidateReferralAuthorizationAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Description of the specialist referral being requested")] string description)
    {
        await Task.Delay(850);
        var failed = _rng.Next(4) == 0; // ~25%
        var result = new
        {
            patientName,
            validationPassed = !failed,
            validationFailed = failed
                ? Pick(
                    "Prior authorization required: patient's HMO plan requires a PCP-initiated referral with auth number before the specialist visit.",
                    "Specialist not in network: the referred provider is out-of-network for the patient's current plan.",
                    "Step therapy required: insurance requires documented failure of conservative treatment before this specialist referral is covered.")
                : (string?)null,
            checkedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    // ── Warning ──────────────────────────────────────────────

    [KernelFunction("CreateClinicalWarning")]
    [Description("Records a clinical warning when a requested action cannot be safely or appropriately completed. Call this instead of the execution tool when any validation tool returns a non-null validationFailed value.")]
    public async Task<string> CreateClinicalWarningAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("The clinical reason why this action cannot be completed, taken directly from the validationFailed field")] string reason)
    {
        await Task.Delay(850);
        var result = new
        {
            warningId      = $"WARN-{_rng.Next(10_000, 99_999)}",
            patientName,
            reason,
            severity       = "Clinical Hold",
            status         = "WarningIssued",
            requiresReview = true,
            issuedAt       = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    // ── Phase 3: Execution ───────────────────────────────────

    [KernelFunction("RefillPrescription")]
    [Description("Submits a medication refill request for an existing prescription to the pharmacy system. Only call this after ValidateMedicationRefill returns validationFailed: null.")]
    public async Task<string> RefillPrescriptionAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Description of the medication refill needed")] string description)
    {
        await Task.Delay(1500);
        var result = new
        {
            rxNumber       = $"RX-{_rng.Next(100_000, 999_999)}",
            pharmacy       = Pick("Walgreens #4821", "CVS Pharmacy #2203", "Rite Aid #0917", "Walmart Pharmacy #3344"),
            status         = "Submitted",
            estimatedReady = "2–4 hours",
            patientName,
            description
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("SubmitLabOrder")]
    [Description("Creates a laboratory order for diagnostic tests in the EHR system. Only call this after ValidateLabOrderIndication returns validationFailed: null.")]
    public async Task<string> SubmitLabOrderAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Description of the lab tests to be ordered")] string description)
    {
        await Task.Delay(1500);
        var result = new
        {
            orderId     = $"LAB-{_rng.Next(10_000, 99_999)}",
            lab         = Pick("Quest Diagnostics", "LabCorp", "BioReference Laboratories", "Hospital Lab"),
            tests       = description,
            status      = "OrderCreated",
            priority    = "Routine",
            patientName
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("SubmitReferralOrder")]
    [Description("Sends a specialist referral order for a patient to the referral management system. Only call this after ValidateReferralAuthorization returns validationFailed: null.")]
    public async Task<string> SubmitReferralOrderAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Description of the specialist referral")] string description)
    {
        await Task.Delay(1500);
        var result = new
        {
            referralNumber = $"REF-{_rng.Next(10_000, 99_999)}",
            specialist     = Pick("Dr. Sarah Chen (Oncology)", "Dr. Raj Patel (Cardiology)", "Dr. Maria Torres (Neurology)", "Dr. James Kim (Endocrinology)"),
            facility       = Pick("City Medical Center", "University Health System", "Regional Specialty Hospital", "Downtown Medical Plaza"),
            status         = "ReferralSent",
            urgency        = "Routine",
            patientName
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("CreateMedicationOrder")]
    [Description("Creates a new medication prescription order in the EHR and routes it to the pharmacy. Only call this after CheckDrugInteractions returns validationFailed: null.")]
    public async Task<string> CreateMedicationOrderAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Description of the medication order including drug name and instructions")] string description)
    {
        await Task.Delay(1500);
        var result = new
        {
            prescriptionId = $"PRX-{_rng.Next(100_000, 999_999)}",
            medication     = description,
            dosage         = Pick("10mg once daily", "25mg twice daily", "50mg as needed", "5mg every 8 hours"),
            daysSupply     = Pick("30", "60", "90"),
            refills        = _rng.Next(0, 4),
            status         = "PrescriptionCreated",
            patientName
        };
        return JsonSerializer.Serialize(result);
    }

    // Randomly selects one item from the provided options.
    // Used to produce varied but realistic-looking demo output so the
    // UI doesn't show the same pharmacy or specialist on every run.
    private static string Pick(params string[] options) =>
        options[_rng.Next(options.Length)];
}

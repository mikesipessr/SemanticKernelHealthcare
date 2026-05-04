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
    //
    // GetLabResults is used exclusively for LabOrder tasks. It returns
    // actual measured values with reference ranges and abnormality flags
    // so the agent can reason about clinical significance rather than
    // just validating a binary pass/fail.

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

    [KernelFunction("GetLabResults")]
    [Description("Retrieves a complete laboratory panel including measured values, reference ranges, and abnormality flags (HIGH/LOW/CRITICAL). Use for LabOrder tasks — results are meant to be analyzed and reasoned about, not just validated.")]
    public async Task<string> GetLabResultsAsync(
        [Description("Full name of the patient")] string patientName)
    {
        await Task.Delay(850);

        // Three distinct clinical scenarios chosen randomly so every demo
        // run shows a different decision path. Jitter keeps numbers realistic.
        double Jitter(double v) => Math.Round(v + (_rng.NextDouble() - 0.5) * 0.6, 1);

        var scenario = _rng.Next(3);

        object[] results = scenario switch
        {
            // Scenario 0 — CRITICAL values across multiple analytes.
            // Combined picture (leukocytosis + severe anemia + thrombocytopenia +
            // hyperkalemia + elevated creatinine) → should trigger EscalateCriticalLabValues.
            0 => new object[]
            {
                new { analyte = "WBC",        value = Jitter(19.2), unit = "K/uL",   referenceRange = "4.0–11.0",  flag = "CRITICAL" },
                new { analyte = "Hgb",        value = Jitter(5.8),  unit = "g/dL",   referenceRange = "12.0–16.0", flag = "CRITICAL" },
                new { analyte = "Platelets",  value = Jitter(38.0), unit = "K/uL",   referenceRange = "150–400",   flag = "CRITICAL" },
                new { analyte = "Potassium",  value = Jitter(6.4),  unit = "mEq/L",  referenceRange = "3.5–5.0",   flag = "CRITICAL" },
                new { analyte = "Creatinine", value = Jitter(3.2),  unit = "mg/dL",  referenceRange = "0.6–1.2",   flag = "CRITICAL" },
                new { analyte = "Sodium",     value = Jitter(138.0),unit = "mEq/L",  referenceRange = "136–145",   flag = (string?)null },
            },
            // Scenario 1 — Abnormal but non-critical (borderline anemia pattern).
            // Elevated WBC + low Hgb + low Platelets + low MCV suggests possible
            // iron-deficiency or inflammatory anemia → OrderAdditionalDiagnostics.
            1 => new object[]
            {
                new { analyte = "WBC",        value = Jitter(12.5), unit = "K/uL",   referenceRange = "4.0–11.0",  flag = "HIGH" },
                new { analyte = "Hgb",        value = Jitter(9.8),  unit = "g/dL",   referenceRange = "12.0–16.0", flag = "LOW"  },
                new { analyte = "Platelets",  value = Jitter(112.0),unit = "K/uL",   referenceRange = "150–400",   flag = "LOW"  },
                new { analyte = "MCV",        value = Jitter(72.0), unit = "fL",     referenceRange = "80–100",    flag = "LOW"  },
                new { analyte = "Sodium",     value = Jitter(138.0),unit = "mEq/L",  referenceRange = "136–145",   flag = (string?)null },
                new { analyte = "Creatinine", value = Jitter(1.1),  unit = "mg/dL",  referenceRange = "0.6–1.2",   flag = (string?)null },
            },
            // Scenario 2 — Normal / near-normal. All values within reference range.
            // No flags → DocumentAndMonitor.
            _ => new object[]
            {
                new { analyte = "WBC",        value = Jitter(7.8),  unit = "K/uL",   referenceRange = "4.0–11.0",  flag = (string?)null },
                new { analyte = "Hgb",        value = Jitter(13.1), unit = "g/dL",   referenceRange = "12.0–16.0", flag = (string?)null },
                new { analyte = "Platelets",  value = Jitter(245.0),unit = "K/uL",   referenceRange = "150–400",   flag = (string?)null },
                new { analyte = "Sodium",     value = Jitter(140.0),unit = "mEq/L",  referenceRange = "136–145",   flag = (string?)null },
                new { analyte = "Potassium",  value = Jitter(4.2),  unit = "mEq/L",  referenceRange = "3.5–5.0",   flag = (string?)null },
                new { analyte = "Creatinine", value = Jitter(0.9),  unit = "mg/dL",  referenceRange = "0.6–1.2",   flag = (string?)null },
                new { analyte = "Glucose",    value = Jitter(95.0), unit = "mg/dL",  referenceRange = "70–100",    flag = (string?)null },
            }
        };

        var result = new
        {
            patientName,
            panelName   = "Comprehensive Metabolic Panel with CBC",
            collectedAt = DateTime.UtcNow.AddHours(-_rng.Next(2, 12)).ToString("o"),
            results,
            retrievedAt = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    // ── LabOrder follow-up decision tools ───────────────────
    //
    // The agent reads GetLabResults output and selects exactly one of
    // these three tools based on clinical significance. No validation
    // pass/fail flag drives the decision — the model must reason about
    // the actual values.

    [KernelFunction("EscalateCriticalLabValues")]
    [Description("Initiates urgent clinical escalation for CRITICAL laboratory values requiring immediate physician notification. Call when any result has a CRITICAL flag, or when the combination of abnormal values indicates acute patient instability (e.g., severe anemia + thrombocytopenia + leukocytosis).")]
    public async Task<string> EscalateCriticalLabValuesAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Comma-separated list of the critical analytes and their values, e.g. 'Hgb 5.8 g/dL (CRITICAL LOW), Platelets 38 K/uL (CRITICAL LOW)'")] string criticalFindings,
        [Description("Brief clinical rationale for the escalation")] string clinicalRationale)
    {
        await Task.Delay(1500);
        var result = new
        {
            escalationId     = $"ESC-{_rng.Next(10_000, 99_999)}",
            patientName,
            criticalFindings,
            clinicalRationale,
            notifiedProvider = $"Dr. {Pick("Williams", "Johnson", "Smith", "Brown", "Garcia")}",
            contactMethod    = Pick("Secure page", "Direct call", "On-call pager"),
            status           = "EscalationInitiated",
            urgency          = "STAT",
            issuedAt         = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("OrderAdditionalDiagnostics")]
    [Description("Orders targeted follow-up diagnostic tests when laboratory results are abnormal (HIGH or LOW flags) but not critical, and further workup would clarify the clinical picture before a treatment decision is made.")]
    public async Task<string> OrderAdditionalDiagnosticsAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("The specific abnormal finding that prompted the additional testing, e.g. 'Hgb 9.8 LOW with MCV 72 LOW'")] string triggeringFinding,
        [Description("Specific follow-up tests to order, e.g. 'Iron panel, ferritin, reticulocyte count, B12/folate'")] string additionalTests)
    {
        await Task.Delay(1500);
        var result = new
        {
            orderId         = $"LAB-{_rng.Next(10_000, 99_999)}",
            patientName,
            triggeringFinding,
            testsOrdered    = additionalTests,
            lab             = Pick("Quest Diagnostics", "LabCorp", "BioReference Laboratories", "Hospital Lab"),
            priority        = "Routine",
            status          = "OrderCreated",
            orderedAt       = DateTime.UtcNow.ToString("o")
        };
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("DocumentAndMonitor")]
    [Description("Documents laboratory results in the patient chart and schedules routine follow-up monitoring when all values are within normal limits or show only clinically insignificant deviation. Call when no immediate intervention is warranted.")]
    public async Task<string> DocumentAndMonitorAsync(
        [Description("Full name of the patient")] string patientName,
        [Description("Brief summary of the lab findings and clinical assessment, e.g. 'CBC and CMP within normal limits; no acute findings'")] string clinicalSummary,
        [Description("Recommended follow-up interval, e.g. '3 months', '6 months', 'annual'")] string followUpInterval)
    {
        await Task.Delay(1000);
        var result = new
        {
            documentationId = $"DOC-{_rng.Next(10_000, 99_999)}",
            patientName,
            clinicalSummary,
            status          = "Documented",
            followUpInterval,
            nextReviewDate  = DateTime.UtcNow.AddMonths(_rng.Next(3, 7)).ToString("yyyy-MM-dd"),
            documentedAt    = DateTime.UtcNow.ToString("o")
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

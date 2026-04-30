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
// for each task type. Vague descriptions cause misrouting.
//
// All four functions are demo stubs: they wait 1.5 seconds
// (simulating real EHR/pharmacy API latency) and return realistic-
// looking JSON rather than calling actual systems. Swapping in real
// implementations means replacing the Task.Delay and fake-data
// logic inside each method — the SK plumbing stays the same.
// ============================================================

using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace SemanticKernelHealthcare.Api.Plugins;

public class HealthcarePlugin
{
    private static readonly Random _rng = new();

    [KernelFunction("RefillPrescription")]
    [Description("Submits a medication refill request for an existing prescription to the pharmacy system.")]
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
    [Description("Creates a laboratory order for diagnostic tests in the EHR system.")]
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
    [Description("Sends a specialist referral order for a patient to the referral management system.")]
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
    [Description("Creates a new medication prescription order in the EHR and routes it to the pharmacy.")]
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

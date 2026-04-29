namespace SemanticKernelHealthcare.Api.Models;

public class TranscribeResponse
{
    public string Transcription { get; set; } = "";
    public List<HealthcareTask> Tasks { get; set; } = [];
}

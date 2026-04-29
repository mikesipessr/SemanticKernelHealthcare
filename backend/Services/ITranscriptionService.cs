namespace SemanticKernelHealthcare.Api.Services;

public interface ITranscriptionService
{
    Task<string> TranscribeAsync(Stream audioStream, string mimeType, CancellationToken ct = default);
}

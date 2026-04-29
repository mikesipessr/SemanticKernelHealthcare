using SemanticKernelHealthcare.Api.Models;

namespace SemanticKernelHealthcare.Api.Services;

public interface ITaskClassificationService
{
    Task<List<HealthcareTask>> ClassifyAsync(string transcription, CancellationToken ct = default);
}

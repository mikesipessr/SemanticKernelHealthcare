// ============================================================
// ITaskClassificationService.cs — Abstraction for AI task extraction
//
// Separating this into an interface follows the same principle as
// ITranscriptionService: it decouples the controller from the
// specific AI model being used and makes the service independently
// testable. A mock implementation can return fixed task lists in
// unit tests without calling the OpenAI API.
// ============================================================

using SemanticKernelHealthcare.Api.Models;

namespace SemanticKernelHealthcare.Api.Services;

public interface ITaskClassificationService
{
    /// <summary>
    /// Analyzes <paramref name="transcription"/> and extracts any
    /// healthcare tasks it contains as structured objects.
    /// </summary>
    /// <param name="transcription">
    ///   The raw text from Whisper's transcription. May contain one
    ///   or more tasks, or none at all (in which case an empty list
    ///   is returned).
    /// </param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>
    ///   A list of <see cref="HealthcareTask"/> objects extracted from
    ///   the transcription. Returns an empty list if no tasks are found
    ///   or if the AI response cannot be parsed.
    /// </returns>
    Task<List<HealthcareTask>> ClassifyAsync(string transcription, CancellationToken ct = default);
}

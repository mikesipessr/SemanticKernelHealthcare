// ============================================================
// ITranscriptionService.cs — Abstraction for audio transcription
//
// Defining an interface rather than using the concrete class
// directly gives us two benefits:
//
//   1. Testability — unit tests can inject a mock that returns
//      fixed text without hitting the OpenAI API.
//
//   2. Replaceability — swapping from OpenAI Whisper to Azure
//      OpenAI or a local Whisper model only requires a new
//      implementation class; the controller and DI registration
//      change, but nothing else does.
// ============================================================

namespace SemanticKernelHealthcare.Api.Services;

public interface ITranscriptionService
{
    /// <summary>
    /// Transcribes the audio in <paramref name="audioStream"/> to text.
    /// </summary>
    /// <param name="audioStream">
    ///   A readable stream containing the raw audio bytes. The caller
    ///   is responsible for disposing the stream.
    /// </param>
    /// <param name="mimeType">
    ///   The MIME type of the audio (e.g. "audio/webm", "audio/mp4",
    ///   "audio/wav"). Used to derive the file extension hint that
    ///   OpenAI's Whisper endpoint requires to identify the codec.
    /// </param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>The transcribed text, or an empty string if silent.</returns>
    Task<string> TranscribeAsync(Stream audioStream, string mimeType, CancellationToken ct = default);
}

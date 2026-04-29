// ============================================================
// TranscriptionService.cs — Speech-to-text via Semantic Kernel
//
// This service wraps Semantic Kernel's IAudioToTextService, which
// in turn calls OpenAI's Whisper model. The abstraction means we
// could switch to Azure OpenAI or a local Whisper deployment by
// changing one line in Program.cs without touching this class.
//
// Key Whisper facts that shape this implementation:
//   - The API identifies the audio codec from the file *extension*
//     in the filename passed to OpenAIAudioToTextExecutionSettings,
//     not from the Content-Type header. So we derive the extension
//     from the MIME type and pass it as part of a synthetic filename.
//   - Accepted formats: flac, m4a, mp3, mp4, mpeg, mpga, oga, ogg,
//     wav, webm. Browsers send webm (Chrome/Edge/Firefox) or mp4
//     (Safari), both of which Whisper handles natively.
//   - Maximum file size: 25 MB. Enforced at the controller layer.
// ============================================================

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AudioToText;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SemanticKernelHealthcare.Api.Services;

// Primary constructor syntax (C# 12) injects IAudioToTextService
// directly. Semantic Kernel registered this in Program.cs via
// AddOpenAIAudioToText(), so ASP.NET Core's DI container provides
// the concrete OpenAI implementation automatically.
public class TranscriptionService(IAudioToTextService audioToText) : ITranscriptionService
{
    public async Task<string> TranscribeAsync(Stream audioStream, string mimeType, CancellationToken ct = default)
    {
        // Derive the file extension from the MIME type.
        // Whisper uses the filename extension — not the Content-Type —
        // to decide how to decode the audio. "audio/webm;codecs=opus"
        // must map to "webm" so Whisper knows to use its WebM decoder.
        var extension = mimeType.StartsWith("audio/webm") ? "webm"
                      : mimeType.StartsWith("audio/mp4")  ? "mp4"
                      : mimeType is "audio/wav" or "audio/wave" ? "wav"
                      : "webm"; // safe fallback — webm is the most common browser format

        // BinaryData.FromStreamAsync reads the entire stream into
        // memory. Semantic Kernel's AudioContent requires BinaryData
        // rather than a raw Stream. The 25 MB request size limit on
        // the controller keeps this allocation bounded.
        var data = await BinaryData.FromStreamAsync(audioStream, ct);

        // AudioContent is Semantic Kernel's transport type for binary
        // audio. It carries the bytes alongside the MIME type string
        // so the connector can set the correct Content-Type when it
        // calls the OpenAI API.
        var audioContent = new AudioContent(data, mimeType: mimeType);

        // OpenAIAudioToTextExecutionSettings lets us tune the Whisper
        // request. The constructor argument is the filename Whisper
        // uses to detect the audio format — this is why we derived
        // the extension above.
        //
        // Language = "en" tells Whisper to transcribe in English rather
        // than auto-detecting the language, which improves accuracy and
        // reduces latency for English-only clinical notes.
        //
        // Temperature = 0.0 means deterministic output — Whisper will
        // always produce the same transcription for the same audio,
        // which makes the results easier to reason about.
        var settings = new OpenAIAudioToTextExecutionSettings($"recording.{extension}")
        {
            Language    = "en",
            Temperature = 0.0f,
        };

        // GetTextContentAsync sends the audio to Whisper and returns
        // a TextContent object. The .Text property holds the transcript.
        // We use null-coalescing to return an empty string rather than
        // null if Whisper returns no transcription (e.g. silent audio).
        var result = await audioToText.GetTextContentAsync(audioContent, settings, cancellationToken: ct);
        return result.Text ?? "";
    }
}

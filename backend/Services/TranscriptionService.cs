using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AudioToText;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SemanticKernelHealthcare.Api.Services;

public class TranscriptionService(IAudioToTextService audioToText) : ITranscriptionService
{
    public async Task<string> TranscribeAsync(Stream audioStream, string mimeType, CancellationToken ct = default)
    {
        var extension = mimeType.StartsWith("audio/webm") ? "webm"
                      : mimeType.StartsWith("audio/mp4")  ? "mp4"
                      : mimeType is "audio/wav" or "audio/wave" ? "wav"
                      : "webm";

        var data = await BinaryData.FromStreamAsync(audioStream, ct);
        var audioContent = new AudioContent(data, mimeType: mimeType);
        var settings = new OpenAIAudioToTextExecutionSettings($"recording.{extension}")
        {
            Language = "en",
            Temperature = 0.0f,
        };

        var result = await audioToText.GetTextContentAsync(audioContent, settings, cancellationToken: ct);
        return result.Text ?? "";
    }
}

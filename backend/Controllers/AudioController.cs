using Microsoft.AspNetCore.Mvc;
using SemanticKernelHealthcare.Api.Models;
using SemanticKernelHealthcare.Api.Services;

namespace SemanticKernelHealthcare.Api.Controllers;

[ApiController]
[Route("api/audio")]
public class AudioController(ITranscriptionService transcription, ITaskClassificationService classification)
    : ControllerBase
{
    [HttpPost("transcribe")]
    [RequestSizeLimit(26_214_400)] // 25 MB — matches OpenAI Whisper file size limit
    public async Task<ActionResult<TranscribeResponse>> Transcribe(IFormFile audio, CancellationToken ct)
    {
        if (audio is null || audio.Length == 0)
            return BadRequest("No audio file provided.");

        await using var stream = audio.OpenReadStream();
        var text = await transcription.TranscribeAsync(stream, audio.ContentType, ct);
        var tasks = await classification.ClassifyAsync(text, ct);

        return Ok(new TranscribeResponse { Transcription = text, Tasks = tasks });
    }
}

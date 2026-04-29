// ============================================================
// AudioController.cs — HTTP endpoint for audio transcription
//
// This controller exposes a single endpoint:
//   POST /api/audio/transcribe
//
// The client sends a multipart form request containing the
// recorded audio file. The controller delegates to two services:
//   1. ITranscriptionService  — converts speech to text via Whisper
//   2. ITaskClassificationService — extracts structured tasks via GPT-4o
//
// Both results are returned together in a single response so the
// frontend only needs to make one round-trip.
// ============================================================

using Microsoft.AspNetCore.Mvc;
using SemanticKernelHealthcare.Api.Models;
using SemanticKernelHealthcare.Api.Services;

namespace SemanticKernelHealthcare.Api.Controllers;

// [ApiController] enables several conventions automatically:
//   - Binding source inference (query, body, form, etc.)
//   - Automatic model validation with 400 Bad Request responses
//   - Problem Details responses for errors (RFC 7807)
//
// [Route("api/audio")] sets the base URL for all actions in this
// controller. Action-level route attributes are appended to this.
[ApiController]
[Route("api/audio")]
public class AudioController(ITranscriptionService transcription, ITaskClassificationService classification)
    : ControllerBase
{
    // ----------------------------------------------------------
    // POST /api/audio/transcribe
    //
    // Accepts a multipart/form-data request with a single file
    // field named "audio". The field name must match the IFormFile
    // parameter name exactly — ASP.NET Core's model binding uses
    // the parameter name to find the matching form field.
    //
    // [RequestSizeLimit] caps the request body at 25 MB, which is
    // OpenAI's hard limit for the Whisper transcription endpoint.
    // Without this, a very large file would reach OpenAI's API and
    // fail there instead of being rejected early with a clear error.
    // ----------------------------------------------------------
    [HttpPost("transcribe")]
    [RequestSizeLimit(26_214_400)] // 25 MB = 26,214,400 bytes
    public async Task<ActionResult<TranscribeResponse>> Transcribe(IFormFile audio, CancellationToken ct)
    {
        // Validate that the client actually sent a file with content.
        // IFormFile.Length is 0 for empty files; null means the field
        // was missing from the form entirely.
        if (audio is null || audio.Length == 0)
            return BadRequest("No audio file provided.");

        // OpenReadStream() returns a forward-only stream over the
        // uploaded file's bytes. We pass this directly to the
        // transcription service rather than loading everything into
        // memory with ReadAllBytesAsync() — the service handles that
        // internally once it knows the full size it needs.
        await using var stream = audio.OpenReadStream();

        // Step 1: Speech-to-text via Semantic Kernel + OpenAI Whisper.
        // We pass the MIME type (e.g. "audio/webm") so the service can
        // tell Whisper which audio format to expect.
        var text = await transcription.TranscribeAsync(stream, audio.ContentType, ct);

        // Step 2: Extract structured HealthcareTask objects from the
        // transcription text using GPT-4o via Semantic Kernel.
        var tasks = await classification.ClassifyAsync(text, ct);

        // Return both results together. The frontend displays the raw
        // transcription text and renders a card for each task.
        return Ok(new TranscribeResponse { Transcription = text, Tasks = tasks });
    }
}

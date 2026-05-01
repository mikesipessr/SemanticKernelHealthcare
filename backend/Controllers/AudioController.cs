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
using Microsoft.AspNetCore.SignalR;
using SemanticKernelHealthcare.Api.Hubs;
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
public class AudioController(
    ITranscriptionService transcription,
    ITaskClassificationService classification,
    IHubContext<TaskExecutionHub> hubContext)
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
    //
    // SignalR progress pushes: two "TranscriptionStatus" messages are
    // sent during processing so the browser can show what stage is
    // running without waiting for the full HTTP response to complete.
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

        // Step 1: Notify the browser that Whisper is running, then transcribe.
        await hubContext.Clients.All.SendAsync("TranscriptionStatus",
            new { message = "Transcribing audio with Whisper…", stage = "transcribing" }, ct);

        var text = await transcription.TranscribeAsync(stream, audio.ContentType, ct);

        // Step 2: Push the completed transcription text immediately so the
        // browser can display it while GPT-4o extracts tasks in the background.
        await hubContext.Clients.All.SendAsync("TranscriptionStatus",
            new { message = "Extracting structured tasks with GPT-4o…", stage = "classifying", transcription = text }, ct);

        var tasks = await classification.ClassifyAsync(text, ct);

        // Return both results together. The HTTP response is still the
        // source of truth for the final state — SignalR is for progress only.
        return Ok(new TranscribeResponse { Transcription = text, Tasks = tasks });
    }
}

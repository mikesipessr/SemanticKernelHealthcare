// ============================================================
// TranscribeResponse.cs — The API response from POST /api/audio/transcribe
//
// Returning both the raw transcription and the extracted tasks in a
// single response keeps the frontend simple: one fetch call gives it
// everything it needs to render the full result page.
//
// If performance became a concern (e.g. the classification step is
// slow), we could split this into two endpoints and stream the
// transcription first, then the tasks. For a demo that tradeoff
// isn't necessary.
// ============================================================

namespace SemanticKernelHealthcare.Api.Models;

public class TranscribeResponse
{
    /// <summary>
    /// The raw text produced by OpenAI Whisper from the audio recording.
    /// Displayed verbatim in the UI so the user can verify accuracy.
    /// </summary>
    public string Transcription { get; set; } = "";

    /// <summary>
    /// The structured tasks GPT-4o identified in the transcription.
    /// May be empty if the recording contained no actionable tasks.
    /// </summary>
    public List<HealthcareTask> Tasks { get; set; } = [];
}

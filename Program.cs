// =============================================================================
//  Semantic Kernel Audio-to-Text Demo
// -----------------------------------------------------------------------------
//  A small console app that:
//    1. Records audio from your default microphone (NAudio) until you press a key
//    2. Saves the recording as a WAV file
//    3. Sends that WAV file to OpenAI's Whisper model through Semantic Kernel's
//       IAudioToTextService abstraction
//    4. Prints the transcribed text to the console
//
//  The goal of this sample is to show the moving parts as plainly as possible,
//  so the code is intentionally linear (no DI container, no extra abstractions).
// =============================================================================

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AudioToText;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NAudio.Wave;

// -----------------------------------------------------------------------------
// 1. Read the OpenAI API key from an environment variable.
//
//    Why env var? It keeps the secret out of source control. On Windows you can
//    set it once with:    setx OPENAI_API_KEY "sk-..."
//    (then open a new terminal so the variable is picked up).
// -----------------------------------------------------------------------------
string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("ERROR: OPENAI_API_KEY environment variable is not set.");
    Console.Error.WriteLine("Set it with:  setx OPENAI_API_KEY \"sk-...\"  and open a new terminal.");
    return 1;
}

// -----------------------------------------------------------------------------
// 2. Pick an input device.
//
//    NAudio enumerates microphones via WaveInEvent.DeviceCount /
//    GetCapabilities. Device 0 is the OS default - usually fine, but listing
//    them helps if recording produces silence (wrong mic chosen, mic muted,
//    or no permissions granted to desktop apps in Windows Settings).
// -----------------------------------------------------------------------------
if (WaveInEvent.DeviceCount == 0)
{
    Console.Error.WriteLine("ERROR: No audio input devices found.");
    Console.Error.WriteLine("Check Settings > Privacy & security > Microphone.");
    return 1;
}

Console.WriteLine("Available input devices:");
for (int i = 0; i < WaveInEvent.DeviceCount; i++)
{
    var caps = WaveInEvent.GetCapabilities(i);
    Console.WriteLine($"  [{i}] {caps.ProductName}");
}

Console.Write($"Pick device [0-{WaveInEvent.DeviceCount - 1}] (Enter for 0): ");
string? deviceInput = Console.ReadLine();
int deviceNumber = int.TryParse(deviceInput, out var d) ? d : 0;

// -----------------------------------------------------------------------------
// 3. Record audio from the chosen microphone into a WAV file.
//
//    We use NAudio's WaveInEvent which raises events on a background thread as
//    audio buffers come in from the OS. WaveFileWriter persists each buffer to
//    disk in standard PCM WAV format - which is exactly what Whisper accepts.
//
//    Format choice: 16 kHz / 16-bit / mono. Whisper was trained on 16 kHz audio
//    and speech doesn't need stereo, so this gives small files and good quality.
//
//    We track total bytes received so you can SEE that audio is actually
//    arriving from the OS - if this stays at 0 while you talk, the problem is
//    in the OS / device layer, not in Semantic Kernel.
// -----------------------------------------------------------------------------
string wavPath = Path.Combine(Path.GetTempPath(), $"sk_recording_{Guid.NewGuid():N}.wav");
long totalBytes = 0;
short peakAmplitude = 0;

// Use a TaskCompletionSource so we can wait for NAudio's recording thread to
// finish flushing its final buffers before we dispose the writer. Without
// this the file can be truncated and end up effectively empty.
var stoppedTcs = new TaskCompletionSource();

var waveIn = new WaveInEvent
{
    DeviceNumber = deviceNumber,
    WaveFormat = new WaveFormat(rate: 16_000, bits: 16, channels: 1),
    BufferMilliseconds = 100, // 100 ms buffers feel responsive when stopping
};

var writer = new WaveFileWriter(wavPath, waveIn.WaveFormat);

waveIn.DataAvailable += (_, e) =>
{
    writer.Write(e.Buffer, 0, e.BytesRecorded);
    totalBytes += e.BytesRecorded;

    // Compute the loudest 16-bit sample in this buffer so we can show a
    // simple level meter. If this stays at 0, the mic is silent.
    for (int i = 0; i < e.BytesRecorded; i += 2)
    {
        short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
        short abs = sample == short.MinValue ? short.MaxValue : Math.Abs(sample);
        if (abs > peakAmplitude) peakAmplitude = abs;
    }
};

waveIn.RecordingStopped += (_, _) =>
{
    writer.Dispose();   // finalises the WAV header with the correct length
    waveIn.Dispose();
    stoppedTcs.TrySetResult();
};

Console.WriteLine();
Console.WriteLine("Press ENTER to start recording...");
Console.ReadLine();

waveIn.StartRecording();
Console.WriteLine("Recording... press ENTER again to stop. (level should move when you talk)");

// Print a tiny live level meter on a background task until recording stops.
using var levelCts = new CancellationTokenSource();
var levelTask = Task.Run(async () =>
{
    while (!levelCts.IsCancellationRequested)
    {
        // Normalise peak (0..32767) into 0..20 hashes.
        int bars = (int)(peakAmplitude / 32767.0 * 20);
        Console.Write($"\rlevel: [{new string('#', bars).PadRight(20)}] bytes: {totalBytes,8}   ");
        peakAmplitude = 0; // reset so the meter shows the *recent* peak
        try { await Task.Delay(100, levelCts.Token); } catch { }
    }
});

Console.ReadLine();
levelCts.Cancel();
try { await levelTask; } catch { }
Console.WriteLine();

waveIn.StopRecording();
// Wait for RecordingStopped to fire and dispose the writer cleanly.
await stoppedTcs.Task;

var fileInfo = new FileInfo(wavPath);
Console.WriteLine($"Saved recording to: {wavPath} ({fileInfo.Length:N0} bytes)");

if (totalBytes == 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("WARNING: No audio bytes were captured.");
    Console.Error.WriteLine("Likely causes:");
    Console.Error.WriteLine("  - Microphone permission blocked: Settings > Privacy & security > Microphone");
    Console.Error.WriteLine("    (make sure 'Let desktop apps access your microphone' is ON)");
    Console.Error.WriteLine("  - Wrong device selected - try a different number from the list above");
    Console.Error.WriteLine("  - Mic muted in the system tray volume mixer");
    return 1;
}

// -----------------------------------------------------------------------------
// 4. Build a Semantic Kernel that knows how to talk to OpenAI's audio-to-text
//    endpoint.
//
//    AddOpenAIAudioToText() registers an IAudioToTextService implementation
//    backed by the OpenAI REST API. "whisper-1" is OpenAI's hosted Whisper
//    model - the only model id currently accepted by the audio/transcriptions
//    endpoint at the time of writing.
// -----------------------------------------------------------------------------
Kernel kernel = Kernel.CreateBuilder()
    .AddOpenAIAudioToText(modelId: "whisper-1", apiKey: apiKey)
    .Build();

// Pull the audio-to-text service back out of the kernel's service container.
// The kernel acts as a thin DI container here - we could also call the service
// directly, but going through the kernel is the idiomatic SK way.
IAudioToTextService audioToText = kernel.GetRequiredService<IAudioToTextService>();

// -----------------------------------------------------------------------------
// 5. Load the WAV file we just recorded into an AudioContent object.
//
//    AudioContent is SK's transport type for binary audio. It needs both the
//    raw bytes and a MIME type so the connector can set the right Content-Type
//    on the HTTP request to OpenAI.
// -----------------------------------------------------------------------------
byte[] audioBytes = await File.ReadAllBytesAsync(wavPath);
var audioContent = new AudioContent(new BinaryData(audioBytes), mimeType: "audio/wav");

// Optional execution settings. Whisper supports several knobs - here we just
// hint that the audio is English, which improves accuracy a little. Leave
// Language null to let Whisper auto-detect.
//
// Note: OpenAI's transcription endpoint accepts response formats "json",
// "verbose_json", "vtt", and "srt" - NOT "text" (despite the API docs
// listing it). The SK connector validates this and will throw
// NotSupportedException for "text". We just leave it unset and let the
// connector pick the default ("json"); it pulls the .Text out of the
// response either way.
var executionSettings = new OpenAIAudioToTextExecutionSettings(Path.GetFileName(wavPath))
{
    Language = "en",
    Temperature = 0.0f, // 0 = most deterministic transcription
};

// -----------------------------------------------------------------------------
// 6. Send the audio to Whisper and print the result.
// -----------------------------------------------------------------------------
Console.WriteLine("Transcribing...");
TextContent transcription = await audioToText.GetTextContentAsync(audioContent, executionSettings);

Console.WriteLine();
Console.WriteLine("---- Transcription ----");
Console.WriteLine(transcription.Text);
Console.WriteLine("-----------------------");

return 0;

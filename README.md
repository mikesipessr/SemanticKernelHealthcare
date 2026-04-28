# Semantic Kernel Audio-to-Text Demo

A tiny .NET 10 console app that records from your microphone and transcribes the
recording using **Microsoft Semantic Kernel** + **OpenAI Whisper**.

It's the companion sample for an upcoming blog post on my personal site — the
goal is to show how little code it takes to wire up a real audio pipeline with
Semantic Kernel's `IAudioToTextService` abstraction.

## What it does

1. Lists every audio input device the OS exposes and lets you pick one
   (Enter for the default).
2. Prompts you to press **Enter** to begin recording.
3. Captures audio from the chosen microphone (16 kHz / 16-bit / mono WAV)
   using [NAudio](https://github.com/naudio/NAudio), and shows a live level
   meter and byte counter so you can see audio is actually arriving.
4. Stops when you press **Enter** again, waits for NAudio's recording
   thread to finish flushing, and writes the WAV file to your temp
   directory. If zero bytes were captured it prints a diagnostic and exits.
5. Builds a `Kernel` with the OpenAI audio-to-text connector
   (`AddOpenAIAudioToText`).
6. Loads the WAV bytes into a Semantic Kernel `AudioContent` object and
   calls `IAudioToTextService.GetTextContentAsync(...)` against the
   `whisper-1` model.
7. Prints the transcribed text to the console.

## Project layout

```
SemanticKernelAudioToText/
├── Program.cs                         # The whole app - heavily commented
├── SemanticKernelAudioToText.csproj   # net10.0 + Microsoft.SemanticKernel + NAudio
├── SemanticKernelAudioToText.slnx
├── blog-post.md                       # Companion blog article
└── README.md                          # You are here
```

## Prerequisites

- **.NET 10 SDK** (the project targets `net10.0`)
- **Windows** with a working microphone (NAudio uses WASAPI under the hood —
  the recording portion is Windows-only; the Semantic Kernel portion is
  cross-platform)
- An **OpenAI API key** with access to the `whisper-1` model

## Setup

1. **Set your OpenAI API key** as an environment variable so it stays out of
   source control:

   ```powershell
   setx OPENAI_API_KEY "sk-your-key-here"
   ```

   `setx` only updates the environment for **future** processes — close any
   existing terminals (and any IDE that was open before you ran it) and
   launch a fresh one. To set the key for just the current PowerShell
   session instead:

   ```powershell
   $env:OPENAI_API_KEY = "sk-your-key-here"
   ```

2. **Restore and build**:

   ```powershell
   dotnet restore
   dotnet build
   ```

## Running

```powershell
dotnet run
```

Then follow the prompts:

```
Available input devices:
  [0] Microphone (Realtek Audio)
  [1] Headset (Some USB Headset)
Pick device [0-1] (Enter for 0):

Press ENTER to start recording...
Recording... press ENTER again to stop. (level should move when you talk)
level: [########            ] bytes:    48000
Saved recording to: C:\Users\you\AppData\Local\Temp\sk_recording_xxxxx.wav (160,044 bytes)
Transcribing...

---- Transcription ----
Hello world, this is a Semantic Kernel audio-to-text demo.
-----------------------
```

## Key APIs used

| API | Purpose |
|-----|---------|
| `WaveInEvent` (NAudio) | Captures microphone samples on a background thread |
| `WaveFileWriter` (NAudio) | Persists samples to a standard PCM WAV file |
| `Kernel.CreateBuilder().AddOpenAIAudioToText(...)` | Registers OpenAI's Whisper connector with the kernel |
| `IAudioToTextService` | The Semantic Kernel abstraction — swap the connector to retarget Azure OpenAI, a local model, etc. |
| `AudioContent` | SK's transport type for binary audio (bytes + MIME type) |
| `OpenAIAudioToTextExecutionSettings` | Optional knobs: `Language`, `ResponseFormat`, `Temperature`, prompt |

## Notes & caveats

- The Semantic Kernel audio-to-text APIs are still tagged **experimental**
  (`SKEXP0001` / `SKEXP0010`). The `.csproj` suppresses those warnings — drop
  the `<NoWarn>` line once they go GA.
- OpenAI's audio endpoint currently caps individual files at **25 MB**. For
  16 kHz / 16-bit mono that's roughly **13 minutes** of audio per request.
- To target **Azure OpenAI** instead, swap `AddOpenAIAudioToText` for
  `AddAzureOpenAIAudioToText` and pass your deployment name and endpoint.
- The SK OpenAI connector's `ResponseFormat` only accepts `json`,
  `verbose_json`, `vtt`, or `srt` — **not** `text`, despite that being a
  valid value in OpenAI's underlying API. Leave it unset and read the
  result from `TextContent.Text`.
- `WaveInEvent.StopRecording()` is asynchronous: don't dispose the
  `WaveFileWriter` until the `RecordingStopped` event fires, or you'll get
  a truncated WAV file with just the header and no audio data.

## Troubleshooting

**"OPENAI_API_KEY environment variable is not set"** — see the note under
Setup; `setx` doesn't affect your current shell or any IDE that was already
open.

**Level meter stays flat / no bytes captured** — the wrong input device is
likely selected, or Windows is blocking microphone access for desktop apps.
Check Settings → Privacy & security → Microphone and make sure
"Let desktop apps access your microphone" is on, and try a different device
number from the list the app prints at startup.

**`NotSupportedException: The audio transcription format 'text' is not
supported`** — see the `ResponseFormat` note above.

## License

MIT — do whatever you like with it.

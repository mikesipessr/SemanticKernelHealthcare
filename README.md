# Semantic Kernel Audio-to-Text Demo

A tiny .NET 10 console app that records from your microphone and transcribes the
recording using **Microsoft Semantic Kernel** + **OpenAI Whisper**.

It's the companion sample for an upcoming blog post on my personal site — the
goal is to show how little code it takes to wire up a real audio pipeline with
Semantic Kernel's `IAudioToTextService` abstraction.

## What it does

1. Prompts you to press **Enter** to begin recording.
2. Captures audio from your default microphone (16 kHz / 16-bit / mono WAV)
   using [NAudio](https://github.com/naudio/NAudio).
3. Stops when you press **Enter** again and writes the WAV file to your temp
   directory.
4. Builds a `Kernel` with the OpenAI audio-to-text connector
   (`AddOpenAIAudioToText`).
5. Loads the WAV bytes into a Semantic Kernel `AudioContent` object and calls
   `IAudioToTextService.GetTextContentAsync(...)` against the `whisper-1` model.
6. Prints the transcribed text to the console.

## Project layout

```
SemanticKernelAudioToText/
├── Program.cs                         # The whole app - heavily commented
├── SemanticKernelAudioToText.csproj   # net10.0 + Microsoft.SemanticKernel + NAudio
├── SemanticKernelAudioToText.slnx
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

   Open a new terminal afterwards so the variable is picked up.

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
Press ENTER to start recording...
Recording... press ENTER again to stop.
Saved recording to: C:\Users\you\AppData\Local\Temp\sk_recording_xxxxx.wav
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

## License

MIT — do whatever you like with it.

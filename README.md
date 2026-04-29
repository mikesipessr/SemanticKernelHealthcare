# SemanticKernelHealthcare

A full-stack healthcare voice assistant demo built with **React** + **ASP.NET Core** + **Microsoft Semantic Kernel**.

Record a clinical note, get an automatic transcription via OpenAI Whisper, and extract structured healthcare task objects using GPT-4o — all wired together with Semantic Kernel's service abstractions.

This is the companion sample for a blog post series. A follow-up post will add an agentic layer that dispatches each task to the appropriate tool (pharmacy system, EHR API, referral service, etc.).

## Architecture

```
Browser (React + Vite)
  │  getUserMedia() → Web Audio API (level meter)
  │  MediaRecorder → audio/webm blob
  │  POST /api/audio/transcribe (multipart)
  ▼
ASP.NET Core Web API (.NET 10)
  ├── TranscriptionService   → Semantic Kernel IAudioToTextService → OpenAI Whisper
  └── TaskClassificationService → Semantic Kernel IChatCompletionService → GPT-4o
  ▼
TranscribeResponse { transcription: string, tasks: HealthcareTask[] }
```

### HealthcareTask model

```csharp
public enum TaskType { MedicationRefill, MedicationOrder, ReferralOrder, LabOrder }

public class HealthcareTask
{
    public TaskType Type { get; set; }
    public string PatientFirstName { get; set; }
    public string PatientLastName { get; set; }
    public string Description { get; set; }
}
```

## Project layout

```
SemanticKernelHealthcare/
├── backend/
│   ├── SemanticKernelHealthcare.Api.csproj
│   ├── Program.cs
│   ├── Controllers/AudioController.cs
│   ├── Models/          HealthcareTask, TaskType, TranscribeResponse
│   └── Services/        TranscriptionService, TaskClassificationService
└── frontend/
    └── src/
        ├── App.tsx
        ├── hooks/        useAudioRecorder, useLevelMeter
        ├── components/   DeviceSelector, RecordButton, LevelMeter,
        │                 TranscriptionDisplay, TaskCard
        ├── api/          transcribeApi.ts
        └── types/        healthcare.ts
```

## Prerequisites

- **.NET 10 SDK**
- **Node.js 18+**
- **OpenAI API key** with access to `whisper-1` and `gpt-4o`
- A modern browser (Chrome, Edge, or Firefox recommended)

## Setup

### API key

**Option A — `appsettings.Development.json`** (gitignored):

Create `backend/appsettings.Development.json`:
```json
{
  "OpenAI": {
    "ApiKey": "sk-your-key-here"
  }
}
```

**Option B — dotnet user-secrets**:
```powershell
cd backend
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-key-here"
```

### Install frontend dependencies

```powershell
cd frontend
npm install
```

## Running in development

Two terminals:

**Terminal 1 — backend:**
```powershell
cd backend
dotnet run
```
API available at `http://localhost:5000`.

**Terminal 2 — frontend:**
```powershell
cd frontend
npm run dev
```
App available at `http://localhost:5173`. Vite proxies `/api/*` to the backend automatically — no CORS configuration needed in the browser.

## API reference

### `POST /api/audio/transcribe`

| Field | Type | Description |
|-------|------|-------------|
| `audio` | multipart file | Audio recording (WebM, MP4, WAV accepted) |

**Response:**
```json
{
  "transcription": "Please schedule a referral for Jane Doe to oncology...",
  "tasks": [
    {
      "type": "ReferralOrder",
      "patientFirstName": "Jane",
      "patientLastName": "Doe",
      "description": "Schedule a referral to oncology for Jane Doe."
    }
  ]
}
```

## Key APIs used

| API | Purpose |
|-----|---------|
| `navigator.mediaDevices.getUserMedia()` | Browser microphone access |
| `MediaRecorder` | Records audio as WebM/Opus blob |
| `AnalyserNode` (Web Audio API) | Powers the real-time level meter |
| `IAudioToTextService` (Semantic Kernel) | Whisper transcription abstraction |
| `IChatCompletionService` (Semantic Kernel) | GPT-4o task extraction abstraction |
| `AddOpenAIAudioToText` / `AddOpenAIChatCompletion` | SK connector registration |

## Notes & caveats

- Audio-to-text APIs in Semantic Kernel are still **experimental** (`SKEXP0001` / `SKEXP0010`). The `.csproj` suppresses those warnings — remove `<NoWarn>` once they go GA.
- OpenAI's Whisper endpoint caps files at **25 MB**. The API enforces this via `[RequestSizeLimit]`.
- Browser audio is recorded as **WebM/Opus** (Chrome/Edge/Firefox) or **MP4/AAC** (Safari). OpenAI's transcription endpoint accepts both natively — no server-side conversion needed.
- The `TaskClassificationService` uses `Temperature = 0.0` for deterministic JSON output. The system prompt instructs the model to return a raw JSON array with no markdown or explanation.
- The `HealthcareTask` objects are intentionally thin — just the data needed for display and future dispatch. The upcoming agentic layer will route each task to the appropriate tool based on `TaskType`.

## Extensibility (upcoming)

The next article will add an `AgentOrchestrationService` that receives the `HealthcareTask` list and dispatches each item to a Semantic Kernel tool per task type:

```
HealthcareTask[] → AgentOrchestrationService
    ├── MedicationRefillTool  (pharmacy system)
    ├── MedicationOrderTool   (EHR API)
    ├── ReferralOrderTool     (referral fax / HL7)
    └── LabOrderTool          (lab requisition system)
```

The `ITaskClassificationService` interface is independently injectable, making it straightforward to wire into that orchestration pipeline.

## License

MIT

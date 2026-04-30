# SemanticKernelHealthcare

A full-stack healthcare voice assistant demo built with **React** + **ASP.NET Core** + **Microsoft Semantic Kernel**.

Record a clinical note, get an automatic transcription via OpenAI Whisper, and extract structured healthcare tasks using GPT-4o. Each task is then dispatched to a Semantic Kernel agent that selects and calls the right tool — with real-time status updates streamed back to the UI via SignalR.

**GitHub:** [mikesipessr/SemanticKernelHealthcare](https://github.com/mikesipessr/SemanticKernelHealthcare)

---

> **This is a demo. Do not use it with real patient data.**
>
> Patient names, clinical notes, and task descriptions are Protected Health Information (PHI). Before sending PHI to any third-party AI service — including OpenAI — you must have a **Business Associate Agreement (BAA)** in place with that vendor. This project has no BAA, no audit logging, no access controls, and no de-identification pipeline. It is for learning and architecture exploration only.

---

## How It Works

1. Click record and speak a clinical note
2. The audio is transcribed by Whisper and structured into tasks by GPT-4o
3. Tasks appear as cards — hit **Run** on any card or **Run All Tasks** to fire them all
4. Each task spins up a Semantic Kernel agent that picks the right tool (`RefillPrescription`, `SubmitLabOrder`, `SubmitReferralOrder`, or `CreateMedicationOrder`) and executes it
5. Real-time updates stream back over SignalR: you can watch the agent select and call tools as it works
6. Completed cards show the tool result, timestamp, and token usage. The Activity Log at the bottom keeps a timestamped feed of every event across all tasks.

## Architecture

```
Browser (React + Vite)
  │  getUserMedia() → MediaRecorder → audio/webm blob
  │  POST /api/audio/transcribe (multipart)
  │
  ▼
ASP.NET Core Web API (.NET 10)
  ├── TranscriptionService       → SK IAudioToTextService  → Whisper
  └── TaskClassificationService  → SK IChatCompletionService → GPT-4o
  │
  ▼
HealthcareTask[] (type, patient, description)
  │
  │  POST /api/tasks/execute  →  202 Accepted
  ▼
AgentOrchestrationService  (one Task.Run per task, concurrent)
  ├── Kernel.Clone() + HealthcarePlugin registered per task
  ├── FunctionChoiceBehavior.Auto() — model selects the tool
  ├── IAutoFunctionInvocationFilter (SignalRInvocationFilter)
  │     fires SignalR push before + after each tool call
  └── IHubContext<TaskExecutionHub>  → SignalR → browser
```

### HealthcarePlugin tools

| KernelFunction | What it simulates |
|---|---|
| `RefillPrescription` | Submits a refill to the pharmacy system |
| `SubmitLabOrder` | Creates a lab requisition in the EHR |
| `SubmitReferralOrder` | Sends a specialist referral |
| `CreateMedicationOrder` | Creates a new prescription |

Each tool has a 1.5-second simulated delay and returns realistic JSON (confirmation numbers, pharmacy names, specialist assignments, etc.).

## Project Layout

```
SemanticKernelHealthcare/
├── backend/
│   ├── Program.cs
│   ├── Controllers/
│   │   ├── AudioController.cs       POST /api/audio/transcribe
│   │   └── TasksController.cs       POST /api/tasks/execute
│   ├── Hubs/
│   │   └── TaskExecutionHub.cs      SignalR hub at /hubs/tasks
│   ├── Models/
│   │   ├── HealthcareTask.cs
│   │   ├── TaskExecutionRequest.cs
│   │   ├── TaskExecutionStatus.cs
│   │   ├── TaskExecutionUpdate.cs
│   │   └── TranscribeResponse.cs
│   ├── Plugins/
│   │   └── HealthcarePlugin.cs      KernelFunction tools
│   └── Services/
│       ├── TranscriptionService.cs
│       ├── TaskClassificationService.cs
│       ├── AgentOrchestrationService.cs   agent + SignalR filter
│       └── IAgentOrchestrationService.cs
└── frontend/
    └── src/
        ├── App.tsx
        ├── hooks/
        │   ├── useAudioRecorder.ts
        │   ├── useLevelMeter.ts
        │   └── useTaskExecution.ts    SignalR connection + state map
        ├── components/
        │   ├── ActivityLog.tsx        real-time event feed
        │   ├── DeviceSelector.tsx
        │   ├── LevelMeter.tsx
        │   ├── RecordButton.tsx
        │   ├── TaskCard.tsx           idle / running / completed / failed states
        │   └── TranscriptionDisplay.tsx
        ├── api/
        │   ├── transcribeApi.ts
        │   └── tasksApi.ts
        └── types/
            └── healthcare.ts
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

**Option B — dotnet user-secrets:**
```powershell
cd backend
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-key-here"
```

### Install frontend dependencies

```powershell
cd frontend
npm install
```

## Running in Development

Two terminals:

**Terminal 1 — backend:**
```powershell
cd backend
dotnet run
```
API available at `http://localhost:5050`.

**Terminal 2 — frontend:**
```powershell
cd frontend
npm run dev
```
App available at `http://localhost:5173`. Vite proxies `/api/*` and `/hubs/*` to the backend automatically.

## API Reference

### `POST /api/audio/transcribe`

| Field | Type | Description |
|---|---|---|
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

### `POST /api/tasks/execute`

Accepts a list of task execution requests. Returns **202 Accepted** immediately — results are delivered via SignalR, not the HTTP response.

```json
[
  {
    "taskId": "uuid",
    "type": "ReferralOrder",
    "patientFirstName": "Jane",
    "patientLastName": "Doe",
    "description": "Schedule a referral to oncology."
  }
]
```

### SignalR — `/hubs/tasks`

Connect and listen for `TaskUpdated` events:

```typescript
connection.on('TaskUpdated', (update: TaskExecutionUpdate) => { ... });
```

`TaskExecutionUpdate` shape:

| Field | Type | Description |
|---|---|---|
| `taskId` | string | Matches the ID in the original request |
| `status` | `Running` \| `Completed` \| `Failed` | Current state |
| `toolName` | string? | Which KernelFunction is being/was called |
| `message` | string | Human-readable status message |
| `details` | string? | Raw JSON result from the tool |
| `promptTokens` | number? | Input tokens used |
| `completionTokens` | number? | Output tokens used |
| `startedAt` | string | ISO timestamp |
| `completedAt` | string? | ISO timestamp |

## Key APIs & Libraries

| API / Library | Purpose |
|---|---|
| `navigator.mediaDevices.getUserMedia()` | Browser microphone access |
| `MediaRecorder` | Records audio as WebM/Opus blob |
| `AnalyserNode` (Web Audio API) | Powers the real-time level meter |
| `IAudioToTextService` (Semantic Kernel) | Whisper transcription abstraction |
| `IChatCompletionService` (Semantic Kernel) | GPT-4o task extraction and agent chat |
| `[KernelFunction]` / `[Description]` | Tool authoring for the healthcare plugin |
| `FunctionChoiceBehavior.Auto()` | Lets the model select tools automatically |
| `IAutoFunctionInvocationFilter` | Intercepts tool calls for real-time SignalR pushes |
| `@microsoft/signalr` | Frontend SignalR client |

## Notes & Caveats

- Audio-to-text APIs in Semantic Kernel are still **experimental** (`SKEXP0001` / `SKEXP0010`). The `.csproj` suppresses those warnings — remove `<NoWarn>` once they go GA.
- `FunctionChoiceBehavior.Auto()` is also experimental (`SKEXP0001`). Same story.
- OpenAI's Whisper endpoint caps files at **25 MB**. The API enforces this via `[RequestSizeLimit]`.
- The SignalR hub broadcasts to `Clients.All` — fine for a single-user demo, but in any real deployment you'd scope pushes to a specific connection or group.
- Token counting uses reflection against the response metadata object. It works, but it's worth watching for breakage on SK version bumps.

## License

MIT

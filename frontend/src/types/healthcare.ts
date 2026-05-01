// ============================================================
// healthcare.ts — TypeScript type definitions for the API contract
//
// These types mirror the C# models in the backend. Keeping them in
// sync is essential: if you add a field on the C# side, add it here
// too so TypeScript catches any missed usages at compile time.
//
// Why string union instead of a TypeScript enum?
// TypeScript enums compile to JavaScript objects and introduce a
// runtime value. String union types ("A" | "B") are purely a
// compile-time construct with zero runtime cost, and they
// serialize/deserialize naturally from the JSON string the API
// returns (e.g. "ReferralOrder" maps directly without conversion).
// ============================================================

// Matches the C# TaskType enum. Each value must be identical to the
// enum member name because ASP.NET Core serializes enums as their
// PascalCase string names when JsonStringEnumConverter is registered.
export type TaskType =
  | 'MedicationRefill'
  | 'MedicationOrder'
  | 'ReferralOrder'
  | 'LabOrder';

// Matches the C# HealthcareTask class.
// Note the camelCase field names — ASP.NET Core's default JSON
// serializer lowercases the first letter of each property name
// (e.g. PatientFirstName → patientFirstName).
export interface HealthcareTask {
  id: string;
  type: TaskType;
  patientFirstName: string;
  patientLastName: string;
  /** Plain-English description of the action required. */
  description: string;
}

// Matches the C# TranscribeResponse class.
// Returned by POST /api/audio/transcribe.
export interface TranscribeResponse {
  /** Raw Whisper transcription of the recorded audio. */
  transcription: string;
  /** Structured tasks extracted from the transcription by GPT-4o. */
  tasks: HealthcareTask[];
}

// Matches C# TaskExecutionStatus enum.
// 'Warned' means the agent completed analysis but determined the action
// should not proceed — distinct from 'Failed' (an unexpected error).
export type TaskExecutionStatus = 'Running' | 'Completed' | 'Warned' | 'Failed';

// Matches C# TaskExecutionUpdate — pushed via SignalR as tasks execute.
//
// For the multi-step pipeline, a typical task receives:
//   - 1 initial Running message (no stepNumber, no toolName)
//   - N Running messages with stepNumber set (one before each tool call)
//   - 1 terminal Completed, Warned, or Failed message (with totalSteps and token counts)
export interface TaskExecutionUpdate {
  taskId: string;
  status: TaskExecutionStatus;
  toolName?: string;
  message: string;
  details?: string;
  promptTokens?: number;
  completionTokens?: number;
  startedAt: string;
  completedAt?: string;
  /** 1-based step counter, present on Running messages only. */
  stepNumber?: number;
  /** Total tool calls made across the full pipeline, present on terminal messages. */
  totalSteps?: number;
}

// Pushed via SignalR "TranscriptionStatus" events during POST /api/audio/transcribe.
// Gives the browser two progress checkpoints:
//   stage = "transcribing" — Whisper call is in flight (no transcription text yet)
//   stage = "classifying"  — Whisper finished; transcription is included so the UI
//                            can show the text while GPT-4o extracts tasks
export interface TranscriptionStatusUpdate {
  message: string;
  stage: 'transcribing' | 'classifying';
  transcription?: string;
}

// Sent to POST /api/tasks/execute.
export interface TaskExecutionRequest {
  taskId: string;
  type: TaskType;
  patientFirstName: string;
  patientLastName: string;
  description: string;
}

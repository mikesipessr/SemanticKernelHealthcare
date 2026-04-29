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

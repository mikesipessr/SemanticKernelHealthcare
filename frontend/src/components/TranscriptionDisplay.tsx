// ============================================================
// TranscriptionDisplay.tsx — Shows Whisper's transcription output
//
// This component has four visual states:
//   hidden        — text is null, not loading, no early transcription
//   skeleton      — loading, Whisper hasn't returned yet
//   early text    — loading, Whisper returned; shows transcription while
//                   GPT-4o extracts tasks (text from SignalR, not HTTP response)
//   final text    — not loading; shows the confirmed transcription from
//                   the HTTP response
//
// The skeleton loader (animated shimmer gradient) provides feedback
// during the transcription API call. Once Whisper completes, the
// `transcriptionStatus` prop carries the transcript text via SignalR
// so the user can read it before the full HTTP response arrives.
//
// We use a <pre> tag to preserve whitespace and line breaks in the
// transcription exactly as Whisper returned them.
// ============================================================

import type { TranscriptionStatusUpdate } from '../types/healthcare';

interface TranscriptionDisplayProps {
  /** The confirmed transcription text from the HTTP response, or null before first recording. */
  text: string | null;
  /** True while the API call is in flight. */
  loading: boolean;
  /** SignalR progress updates from the backend pipeline — carries early transcription text. */
  transcriptionStatus?: TranscriptionStatusUpdate | null;
}

export function TranscriptionDisplay({ text, loading, transcriptionStatus }: TranscriptionDisplayProps) {
  // Prefer the confirmed HTTP response text; fall back to the early Whisper
  // result from SignalR so it appears before the full response arrives.
  const displayText = text ?? transcriptionStatus?.transcription ?? null;

  // Stay hidden until we have something to show.
  if (!loading && displayText === null) return null;

  return (
    <section className="transcription-section">
      <h2>Transcription</h2>
      {!displayText ? (
        // No text yet — Whisper is still running. Show the shimmer skeleton.
        <div className="skeleton" />
      ) : (
        // Text available (either early from SignalR or final from HTTP).
        // <pre> preserves whitespace and newlines from the transcript.
        <pre className="transcription-text">{displayText}</pre>
      )}
      {loading && transcriptionStatus?.message && (
        // Status message shown while the pipeline is still running.
        // Disappears once the HTTP response arrives and loading becomes false.
        <p className="transcription-status">{transcriptionStatus.message}</p>
      )}
    </section>
  );
}

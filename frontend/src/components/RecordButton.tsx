// ============================================================
// RecordButton.tsx — Context-aware record/stop/loading button
//
// Rather than a single button with conditional text, this component
// renders a completely different button element for each recorder
// state. That approach gives us clean CSS classes and prevents
// accidental double-clicks from triggering the wrong action.
//
// State → UI mapping:
//   'idle'       → blue "Start Recording" button
//   'recording'  → red "Stop Recording" button with pulsing dot
//   'processing' → grey disabled "Transcribing..." button with spinner
// ============================================================

import type { RecorderState } from '../hooks/useAudioRecorder';

interface RecordButtonProps {
  /** Current phase of the recorder. Drives which button is rendered. */
  state: RecorderState;
  /** Called when the user clicks Start Recording (idle state only). */
  onStart: () => void;
  /** Called when the user clicks Stop Recording (recording state only). */
  onStop: () => void;
}

export function RecordButton({ state, onStart, onStop }: RecordButtonProps) {
  // Processing: show a disabled button so the user knows something is
  // happening. The spinner CSS animation provides visual feedback.
  if (state === 'processing') {
    return (
      <button className="btn btn-processing" disabled>
        <span className="spinner" /> Transcribing...
      </button>
    );
  }

  // Recording: the pulsing red dot signals active capture.
  // Clicking calls onStop, which triggers stopRecording() in the hook.
  if (state === 'recording') {
    return (
      <button className="btn btn-stop" onClick={onStop}>
        <span className="record-dot pulsing" /> Stop Recording
      </button>
    );
  }

  // Idle (default): ready to start a new recording.
  return (
    <button className="btn btn-start" onClick={onStart}>
      <span className="record-dot" /> Start Recording
    </button>
  );
}

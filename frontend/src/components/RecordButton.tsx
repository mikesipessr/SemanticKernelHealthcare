import type { RecorderState } from '../hooks/useAudioRecorder';

interface RecordButtonProps {
  state: RecorderState;
  onStart: () => void;
  onStop: () => void;
}

export function RecordButton({ state, onStart, onStop }: RecordButtonProps) {
  if (state === 'processing') {
    return (
      <button className="btn btn-processing" disabled>
        <span className="spinner" /> Transcribing...
      </button>
    );
  }

  if (state === 'recording') {
    return (
      <button className="btn btn-stop" onClick={onStop}>
        <span className="record-dot pulsing" /> Stop Recording
      </button>
    );
  }

  return (
    <button className="btn btn-start" onClick={onStart}>
      <span className="record-dot" /> Start Recording
    </button>
  );
}

// ============================================================
// TranscriptionDisplay.tsx — Shows Whisper's transcription output
//
// This component has three visual states:
//   hidden   — text is null and loading is false (before first recording)
//   skeleton — loading is true (API call in progress)
//   text     — loading is false and text is a non-null string
//
// The skeleton loader (animated shimmer gradient) provides feedback
// during the transcription + classification API call, which can take
// a few seconds for longer recordings.
//
// We use a <pre> tag to preserve whitespace and line breaks in the
// transcription exactly as Whisper returned them.
// ============================================================

interface TranscriptionDisplayProps {
  /** The transcribed text, or null before the first recording completes. */
  text: string | null;
  /** True while the API call is in flight. Shows a skeleton placeholder. */
  loading: boolean;
}

export function TranscriptionDisplay({ text, loading }: TranscriptionDisplayProps) {
  // Stay hidden until we have something to show — either a loading
  // state or real text. This avoids rendering an empty section heading.
  if (!loading && text === null) return null;

  return (
    <section className="transcription-section">
      <h2>Transcription</h2>
      {loading ? (
        // Animated shimmer rectangle shown while waiting for the API.
        // CSS class "skeleton" applies the gradient sweep animation.
        <div className="skeleton" />
      ) : (
        // <pre> preserves whitespace and newlines from the transcript.
        // font-family: inherit in CSS overrides the browser's monospace
        // default so it matches the rest of the UI.
        <pre className="transcription-text">{text}</pre>
      )}
    </section>
  );
}

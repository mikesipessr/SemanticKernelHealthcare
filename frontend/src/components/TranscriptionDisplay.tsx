interface TranscriptionDisplayProps {
  text: string | null;
  loading: boolean;
}

export function TranscriptionDisplay({ text, loading }: TranscriptionDisplayProps) {
  if (!loading && text === null) return null;

  return (
    <section className="transcription-section">
      <h2>Transcription</h2>
      {loading ? (
        <div className="skeleton" />
      ) : (
        <pre className="transcription-text">{text}</pre>
      )}
    </section>
  );
}

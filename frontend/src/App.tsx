import { useState } from 'react';
import './App.css';
import { transcribeAudio } from './api/transcribeApi';
import { DeviceSelector } from './components/DeviceSelector';
import { LevelMeter } from './components/LevelMeter';
import { RecordButton } from './components/RecordButton';
import { TaskCard } from './components/TaskCard';
import { TranscriptionDisplay } from './components/TranscriptionDisplay';
import { useAudioRecorder } from './hooks/useAudioRecorder';
import { useLevelMeter } from './hooks/useLevelMeter';
import type { HealthcareTask } from './types/healthcare';

function App() {
  const [transcription, setTranscription] = useState<string | null>(null);
  const [tasks, setTasks] = useState<HealthcareTask[]>([]);
  const [apiError, setApiError] = useState<string | null>(null);

  const {
    state,
    analyserNode,
    devices,
    selectedDeviceId,
    setSelectedDeviceId,
    startRecording,
    stopRecording,
    error: recorderError,
  } = useAudioRecorder();

  const level = useLevelMeter(analyserNode);
  const isProcessing = state === 'processing';

  const handleStop = async () => {
    setApiError(null);
    try {
      const blob = await stopRecording();
      const result = await transcribeAudio(blob);
      setTranscription(result.transcription);
      setTasks(result.tasks);
    } catch (err) {
      setApiError(err instanceof Error ? err.message : 'An error occurred during transcription.');
    }
  };

  const error = recorderError ?? apiError;

  return (
    <div className="app">
      <header className="app-header">
        <h1>Healthcare Voice Assistant</h1>
        <p className="app-subtitle">Record a clinical note and extract structured tasks automatically</p>
      </header>

      <main className="app-main">
        <section className="recorder-section">
          <DeviceSelector
            devices={devices}
            selectedDeviceId={selectedDeviceId}
            onChange={setSelectedDeviceId}
            disabled={state !== 'idle'}
          />

          <div className="recorder-controls">
            <RecordButton state={state} onStart={startRecording} onStop={handleStop} />
            <LevelMeter level={level} active={state === 'recording'} />
          </div>

          {error && <p className="error-message">{error}</p>}
        </section>

        <TranscriptionDisplay text={transcription} loading={isProcessing} />

        {tasks.length > 0 && (
          <section className="tasks-section">
            <h2>Extracted Tasks</h2>
            <div className="task-grid">
              {tasks.map((task, i) => (
                <TaskCard key={i} task={task} />
              ))}
            </div>
          </section>
        )}
      </main>
    </div>
  );
}

export default App;

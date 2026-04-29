// ============================================================
// App.tsx — Root component and application orchestrator
//
// App is the top-level component that wires together all the
// pieces: recorder state, API calls, and UI components.
//
// Data flow:
//   1. useAudioRecorder manages the mic, MediaRecorder, and AudioContext.
//   2. useLevelMeter reads the AnalyserNode to produce a 0..1 level value.
//   3. When the user stops recording, handleStop:
//        a. Awaits the audio Blob from stopRecording()
//        b. Posts it to the backend via transcribeAudio()
//        c. Stores the transcription text and task list in state
//        d. Calls setIdle() to return the recorder to its ready state
//   4. Child components render based on the current state values.
// ============================================================

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
  // Results from the transcription API call.
  const [transcription, setTranscription] = useState<string | null>(null);
  const [tasks, setTasks]                 = useState<HealthcareTask[]>([]);

  // Separate API error state from the recorder's own error state so
  // we can clear it independently at the start of each new recording.
  const [apiError, setApiError] = useState<string | null>(null);

  // useAudioRecorder handles the entire browser audio pipeline.
  // See hooks/useAudioRecorder.ts for full documentation.
  const {
    state,            // 'idle' | 'recording' | 'processing'
    analyserNode,     // live AnalyserNode while recording, null otherwise
    devices,          // available microphone devices
    selectedDeviceId,
    setSelectedDeviceId,
    startRecording,
    stopRecording,
    setIdle,          // called after the API returns to reset to 'idle'
    error: recorderError,
  } = useAudioRecorder();

  // useLevelMeter reads frequency data from the AnalyserNode on every
  // animation frame and returns a normalized 0..1 amplitude value.
  const level = useLevelMeter(analyserNode);

  // Derived flag used by TranscriptionDisplay to show the skeleton loader.
  const isProcessing = state === 'processing';

  // ----------------------------------------------------------
  // handleStop — called when the user clicks "Stop Recording"
  //
  // This is the core async flow of the application:
  //   1. Stop the MediaRecorder and get the audio Blob.
  //   2. POST the Blob to the backend.
  //   3. Display the transcription and any extracted tasks.
  //   4. Return to idle state regardless of success or failure.
  // ----------------------------------------------------------
  const handleStop = async () => {
    setApiError(null);
    try {
      // stopRecording() halts the MediaRecorder, tears down the
      // AudioContext, and resolves with the complete audio Blob.
      // It also sets recorder state to 'processing' internally.
      const blob = await stopRecording();

      // Send the audio to the backend. The backend calls Whisper
      // for transcription and GPT-4o for task classification,
      // then returns both results in a single response.
      const result = await transcribeAudio(blob);

      setTranscription(result.transcription);
      setTasks(result.tasks);
    } catch (err) {
      setApiError(err instanceof Error ? err.message : 'An error occurred during transcription.');
    } finally {
      // Always return to idle — whether the API call succeeded or
      // failed. Without this the button would stay in 'processing'
      // state indefinitely if an error occurs.
      setIdle();
    }
  };

  // Merge recorder errors (e.g. mic denied) with API errors into a
  // single display value. Recorder errors take priority.
  const error = recorderError ?? apiError;

  return (
    <div className="app">
      <header className="app-header">
        <h1>Healthcare Voice Assistant</h1>
        <p className="app-subtitle">Record a clinical note and extract structured tasks automatically</p>
      </header>

      <main className="app-main">
        {/* ── Recording controls ── */}
        <section className="recorder-section">
          {/* Microphone device picker — hidden until devices are enumerated */}
          <DeviceSelector
            devices={devices}
            selectedDeviceId={selectedDeviceId}
            onChange={setSelectedDeviceId}
            disabled={state !== 'idle'} // lock during recording/processing
          />

          <div className="recorder-controls">
            {/* Button renders differently for each recorder state */}
            <RecordButton state={state} onStart={startRecording} onStop={handleStop} />

            {/* Level meter is only animated while the mic is open */}
            <LevelMeter level={level} active={state === 'recording'} />
          </div>

          {/* Show any error below the controls */}
          {error && <p className="error-message">{error}</p>}
        </section>

        {/* ── Transcription output ── */}
        {/* Hidden before the first recording; shows skeleton while processing */}
        <TranscriptionDisplay text={transcription} loading={isProcessing} />

        {/* ── Extracted task cards ── */}
        {/* Only rendered when GPT-4o found at least one task */}
        {tasks.length > 0 && (
          <section className="tasks-section">
            <h2>Extracted Tasks</h2>
            <div className="task-grid">
              {tasks.map((task, i) => (
                // Using array index as key is acceptable here because
                // the task list is replaced wholesale on each recording,
                // never partially updated.
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

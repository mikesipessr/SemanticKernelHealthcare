import { useState } from 'react';
import './App.css';
import { transcribeAudio } from './api/transcribeApi';
import { ActivityLog } from './components/ActivityLog';
import { DeviceSelector } from './components/DeviceSelector';
import { LevelMeter } from './components/LevelMeter';
import { RecordButton } from './components/RecordButton';
import { TaskCard } from './components/TaskCard';
import { TranscriptionDisplay } from './components/TranscriptionDisplay';
import { useAudioRecorder } from './hooks/useAudioRecorder';
import { useLevelMeter } from './hooks/useLevelMeter';
import { useTaskExecution } from './hooks/useTaskExecution';
import type { HealthcareTask, TaskExecutionRequest } from './types/healthcare';

function App() {
  const [transcription, setTranscription] = useState<string | null>(null);
  const [tasks, setTasks]                 = useState<HealthcareTask[]>([]);
  const [apiError, setApiError]           = useState<string | null>(null);

  const {
    state,
    analyserNode,
    devices,
    selectedDeviceId,
    setSelectedDeviceId,
    startRecording,
    stopRecording,
    setIdle,
    error: recorderError,
  } = useAudioRecorder();

  const level = useLevelMeter(analyserNode);
  const { executionState, activityLog, runTasks, clearLog, transcriptionStatus, clearTranscriptionStatus } = useTaskExecution();

  const isProcessing = state === 'processing';

  const handleStop = async () => {
    setApiError(null);
    clearTranscriptionStatus();
    try {
      const blob = await stopRecording();
      const result = await transcribeAudio(blob);
      setTranscription(result.transcription);
      setTasks(result.tasks.map(t => ({ ...t, id: crypto.randomUUID() })));
    } catch (err) {
      setApiError(err instanceof Error ? err.message : 'An error occurred during transcription.');
    } finally {
      setIdle();
    }
  };

  const handleRunTask = async (task: HealthcareTask) => {
    const req: TaskExecutionRequest = {
      taskId:           task.id,
      type:             task.type,
      patientFirstName: task.patientFirstName,
      patientLastName:  task.patientLastName,
      description:      task.description,
    };
    await runTasks([req]);
  };

  const handleRunAll = async () => {
    const requests: TaskExecutionRequest[] = tasks
      .filter(t => {
        const s = executionState[t.id]?.status;
        return s !== 'Completed' && s !== 'Running' && s !== 'Warned';
      })
      .map(t => ({
        taskId:           t.id,
        type:             t.type,
        patientFirstName: t.patientFirstName,
        patientLastName:  t.patientLastName,
        description:      t.description,
      }));
    if (requests.length === 0) return;
    await runTasks(requests);
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

        <TranscriptionDisplay
          text={transcription}
          loading={isProcessing}
          transcriptionStatus={transcriptionStatus}
        />

        {tasks.length > 0 && (
          <section className="tasks-section">
            <div className="tasks-header">
              <h2>Extracted Tasks</h2>
              <button className="btn btn-run-all" onClick={handleRunAll}>
                ▶▶ Run All Tasks
              </button>
            </div>
            <div className="task-grid">
              {tasks.map(task => (
                <TaskCard
                  key={task.id}
                  task={task}
                  execution={executionState[task.id]}
                  onRun={() => handleRunTask(task)}
                />
              ))}
            </div>
          </section>
        )}

        <ActivityLog
          entries={activityLog}
          tasks={tasks}
          onClear={clearLog}
        />
      </main>
    </div>
  );
}

export default App;

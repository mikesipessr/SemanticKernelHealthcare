// useTaskExecution — manages the SignalR connection and per-task execution state
//
// Responsibilities:
//   - Opens a single HubConnection to /hubs/tasks on mount and closes it on unmount.
//   - Maintains `executionState`: a map of taskId → latest TaskExecutionUpdate,
//     used by TaskCard to render the correct visual state for each card.
//   - Maintains `activityLog`: an append-only list of every update received,
//     used by ActivityLog to show a timestamped feed of all agent events.
//   - Exposes `runTasks` to fire a batch of tasks at the API and clear any
//     stale execution state for those task IDs before the new run starts.
//
// The multi-step pipeline produces one Running update per tool call, then
// exactly one terminal (Completed / Warned / Failed) message. No merge
// logic is needed — each incoming update simply replaces the card's state.

import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { executeTasks } from '../api/tasksApi';
import type { TaskExecutionRequest, TaskExecutionUpdate, TranscriptionStatusUpdate } from '../types/healthcare';

export type ExecutionStateMap = Record<string, TaskExecutionUpdate>;

export function useTaskExecution() {
  const [executionState, setExecutionState] = useState<ExecutionStateMap>({});
  const [activityLog, setActivityLog] = useState<TaskExecutionUpdate[]>([]);
  const [transcriptionStatus, setTranscriptionStatus] = useState<TranscriptionStatusUpdate | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/tasks')
      .withAutomaticReconnect()
      .build();

    connection.on('TaskUpdated', (update: TaskExecutionUpdate) => {
      // Plain replacement — the filter only sends Running messages, so there
      // is never a case where we need to merge two terminal states for the
      // same task. Each update simply advances the card to its latest state.
      setExecutionState(prev => ({ ...prev, [update.taskId]: update }));

      // Every update goes into the activity log regardless of status,
      // giving ActivityLog a full play-by-play of all agent events.
      setActivityLog(prev => [...prev, update]);
    });

    // Progress updates from AudioController during the transcription pipeline.
    // Two messages fire per recording: one when Whisper starts, one when it
    // finishes (carrying the transcription text so the UI can show it early).
    connection.on('TranscriptionStatus', (update: TranscriptionStatusUpdate) => {
      setTranscriptionStatus(update);
    });

    connection.start().catch(console.error);
    connectionRef.current = connection;

    return () => { connection.stop(); };
  }, []);

  const runTasks = useCallback(async (requests: TaskExecutionRequest[]) => {
    // Clear stale state for the tasks we're about to run so their cards
    // reset to idle before the first SignalR update arrives. Without this,
    // a re-run would briefly show the previous completed/warned state.
    setExecutionState(prev => {
      const next = { ...prev };
      requests.forEach(r => delete next[r.taskId]);
      return next;
    });
    await executeTasks(requests);
  }, []);

  const clearLog = useCallback(() => setActivityLog([]), []);

  // Called by App.tsx when a new recording starts so stale status from a
  // previous recording doesn't flash briefly before the new one arrives.
  const clearTranscriptionStatus = useCallback(() => setTranscriptionStatus(null), []);

  return { executionState, activityLog, runTasks, clearLog, transcriptionStatus, clearTranscriptionStatus };
}

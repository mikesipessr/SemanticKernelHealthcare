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

import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { executeTasks } from '../api/tasksApi';
import type { TaskExecutionRequest, TaskExecutionUpdate } from '../types/healthcare';

export type ExecutionStateMap = Record<string, TaskExecutionUpdate>;

export function useTaskExecution() {
  const [executionState, setExecutionState] = useState<ExecutionStateMap>({});
  const [activityLog, setActivityLog] = useState<TaskExecutionUpdate[]>([]);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/tasks')
      .withAutomaticReconnect()
      .build();

    connection.on('TaskUpdated', (update: TaskExecutionUpdate) => {
      setExecutionState(prev => {
        const current = prev[update.taskId];

        // Two-phase completion merge:
        // AgentOrchestrationService sends two Completed messages per task —
        // the first (from SignalRInvocationFilter) carries ToolName + Details,
        // the second carries token counts. If we blindly replaced the state,
        // the second message would overwrite ToolName/Details with undefined.
        // Instead, when both the incoming and current state are Completed, we
        // merge: prefer the incoming values but fall back to the current ones
        // for fields that aren't set in the new message.
        if (current?.status === 'Completed' && update.status === 'Completed') {
          return {
            ...prev,
            [update.taskId]: {
              ...update,
              toolName: update.toolName ?? current.toolName,
              details:  update.details  ?? current.details,
            },
          };
        }
        return { ...prev, [update.taskId]: update };
      });

      // Every update goes into the activity log regardless of status,
      // giving ActivityLog a full play-by-play of all agent events.
      setActivityLog(prev => [...prev, update]);
    });

    connection.start().catch(console.error);
    connectionRef.current = connection;

    return () => { connection.stop(); };
  }, []);

  const runTasks = useCallback(async (requests: TaskExecutionRequest[]) => {
    // Clear stale state for the tasks we're about to run so their cards
    // reset to idle before the first SignalR update arrives. Without this,
    // a re-run would briefly show the previous completed state.
    setExecutionState(prev => {
      const next = { ...prev };
      requests.forEach(r => delete next[r.taskId]);
      return next;
    });
    await executeTasks(requests);
  }, []);

  const clearLog = useCallback(() => setActivityLog([]), []);

  return { executionState, activityLog, runTasks, clearLog };
}

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
        // If the card is already Completed with details, merge rather than overwrite:
        // the summary message carries token counts but we want to keep the tool name + details.
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

      setActivityLog(prev => [...prev, update]);
    });

    connection.start().catch(console.error);
    connectionRef.current = connection;

    return () => { connection.stop(); };
  }, []);

  const runTasks = useCallback(async (requests: TaskExecutionRequest[]) => {
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

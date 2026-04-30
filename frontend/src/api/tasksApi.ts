import type { TaskExecutionRequest } from '../types/healthcare';

export async function executeTasks(requests: TaskExecutionRequest[]): Promise<void> {
  const response = await fetch('/api/tasks/execute', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(requests),
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Execute failed (${response.status}): ${text}`);
  }
}

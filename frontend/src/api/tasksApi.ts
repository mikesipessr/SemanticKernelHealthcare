import type { TaskExecutionRequest } from '../types/healthcare';

// POST /api/tasks/execute
//
// The server returns 202 Accepted immediately and streams results via
// SignalR — there is no response body to parse. We only need to throw
// if the request itself was rejected (4xx/5xx) so the caller can surface
// the error before the SignalR connection would time out silently.
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

// ActivityLog — real-time timestamped feed of all agent events
//
// Receives every TaskExecutionUpdate pushed via SignalR and renders
// them as a scrolling list. For the multi-step pipeline, each task
// produces several entries: one per tool call (Running) plus one
// terminal (Completed / Warned / Failed).
//
// Each entry shows:
//   - Timestamp (from startedAt)
//   - Status icon (⟳ running / ✓ completed / ⚠ warned / ✕ failed)
//   - A human-readable label derived from the task type and patient name
//   - A message with step number prefix for tool-call entries
//   - Token counts on terminal Completed entries
//
// The log auto-scrolls to the bottom as new entries arrive.

import { useEffect, useRef } from 'react';
import type { HealthcareTask, TaskExecutionUpdate } from '../types/healthcare';

interface ActivityLogProps {
  entries: TaskExecutionUpdate[];
  tasks: HealthcareTask[];
  onClear: () => void;
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString([], {
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

// Builds a readable label for a log entry by looking up the task
// from the tasks array. Falls back to a truncated taskId if the
// task isn't found (e.g. if the log entry arrived before tasks loaded).
function taskLabel(taskId: string, tasks: HealthcareTask[]) {
  const t = tasks.find(t => t.id === taskId);
  if (!t) return taskId.slice(0, 8);
  const name = [t.patientFirstName, t.patientLastName].filter(Boolean).join(' ') || 'Unknown';
  // Insert spaces before capital letters to turn "ReferralOrder" → "Referral Order"
  const type = t.type.replace(/([A-Z])/g, ' $1').trim();
  return `${type} · ${name}`;
}

function entryIcon(entry: TaskExecutionUpdate) {
  if (entry.status === 'Failed')    return <span className="log-icon log-icon-error">✕</span>;
  if (entry.status === 'Completed') return <span className="log-icon log-icon-success">✓</span>;
  if (entry.status === 'Warned')    return <span className="log-icon log-icon-warned">⚠</span>;
  return <span className="log-icon log-icon-running">⟳</span>;
}

// Renders token counts as a small badge on terminal Completed entries.
// Returns null if neither count is present.
function tokenBadge(entry: TaskExecutionUpdate) {
  if (entry.promptTokens == null && entry.completionTokens == null) return null;
  const total = (entry.promptTokens ?? 0) + (entry.completionTokens ?? 0);
  return (
    <span className="log-tokens">
      {entry.promptTokens ?? 0}↑ {entry.completionTokens ?? 0}↓ = {total} tokens
    </span>
  );
}

// Formats the message column for a log entry.
// Running entries with stepNumber show "→ [Step N] Calling X…";
// Warned entries show "⚠ <message>"; others follow existing arrow conventions.
function logMessage(entry: TaskExecutionUpdate): string {
  const step = entry.stepNumber ? `[Step ${entry.stepNumber}] ` : '';
  if (entry.status === 'Running') {
    return entry.toolName
      ? `→ ${step}Calling ${entry.toolName}…`
      : entry.message;
  }
  if (entry.status === 'Completed') {
    return entry.toolName
      ? `← ${entry.toolName} completed`
      : entry.message;
  }
  if (entry.status === 'Warned') return `⚠ ${entry.message}`;
  return entry.message; // Failed
}

export function ActivityLog({ entries, tasks, onClear }: ActivityLogProps) {
  const bottomRef = useRef<HTMLDivElement>(null);

  // Scroll to the bottom whenever a new entry is added.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [entries.length]);

  return (
    <section className="activity-log-section">
      <div className="activity-log-header">
        <h2>Agent Activity Log</h2>
        {entries.length > 0 && (
          <button className="btn activity-log-clear" onClick={onClear}>
            Clear
          </button>
        )}
      </div>

      <div className="activity-log">
        {entries.length === 0 && (
          <p className="activity-log-empty">
            Run tasks to see agent activity here.
          </p>
        )}

        {entries.map((entry, i) => (
          <div key={i} className={`log-entry log-entry-${entry.status.toLowerCase()}`}>
            <span className="log-time">{formatTime(entry.startedAt)}</span>
            {entryIcon(entry)}
            <span className="log-label">[{taskLabel(entry.taskId, tasks)}]</span>
            <span className="log-message">{logMessage(entry)}</span>
            {entry.status === 'Completed' && tokenBadge(entry)}
          </div>
        ))}

        {/* Invisible anchor div that scrollIntoView targets on new entries. */}
        <div ref={bottomRef} />
      </div>
    </section>
  );
}

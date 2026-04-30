// TaskCard — displays a single healthcare task and its execution state
//
// Renders one of four visual states based on the `execution` prop:
//
//   idle      (execution is undefined)  — shows the ▶ Run button
//   running   (status === 'Running')    — spinner + current tool name if known
//   completed (status === 'Completed')  — checkmark, tool name, timestamp,
//                                         token counts, collapsible JSON panel
//   failed    (status === 'Failed')     — ✕ icon + error message
//
// The card CSS class (running / completed / failed) drives border/background
// color changes defined in App.css.

import { useState } from 'react';
import type { HealthcareTask, TaskExecutionUpdate, TaskType } from '../types/healthcare';

// Background colors for task type badges. Kept here rather than in CSS
// so the mapping is colocated with the LABEL map and easy to extend when
// new task types are added to the TaskType union.
const BADGE_COLORS: Record<TaskType, string> = {
  MedicationRefill: '#2e7d32',
  MedicationOrder:  '#1565c0',
  ReferralOrder:    '#e65100',
  LabOrder:         '#6a1b9a',
};

// Human-readable labels for each TaskType, used in the badge.
const LABEL: Record<TaskType, string> = {
  MedicationRefill: 'Medication Refill',
  MedicationOrder:  'Medication Order',
  ReferralOrder:    'Referral Order',
  LabOrder:         'Lab Order',
};

interface TaskCardProps {
  task: HealthcareTask;
  execution?: TaskExecutionUpdate;
  onRun: () => void;
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

// Pretty-prints the JSON string returned by the tool, falling back to the
// raw string if it isn't valid JSON (shouldn't happen, but defensive).
function prettyJson(raw: string) {
  try { return JSON.stringify(JSON.parse(raw), null, 2); }
  catch { return raw; }
}

export function TaskCard({ task, execution, onRun }: TaskCardProps) {
  const [showDetails, setShowDetails] = useState(false);

  const patientName =
    [task.patientFirstName, task.patientLastName].filter(Boolean).join(' ') || 'Unknown Patient';

  // Maps execution status to a CSS modifier class on the card wrapper.
  // An absent execution prop (idle state) leaves the class empty.
  const statusClass = execution
    ? execution.status === 'Running'   ? 'running'
    : execution.status === 'Completed' ? 'completed'
    : 'failed'
    : '';

  return (
    <div className={`task-card ${statusClass}`}>
      <span className="task-badge" style={{ backgroundColor: BADGE_COLORS[task.type] }}>
        {LABEL[task.type]}
      </span>

      <h3 className="task-patient">{patientName}</h3>
      <p className="task-description">{task.description}</p>

      {/* ── Execution status area ── */}

      {!execution && (
        <button className="btn btn-run" onClick={onRun}>
          ▶ Run
        </button>
      )}

      {execution?.status === 'Running' && (
        <div className="task-status-row">
          <span className="spinner" />
          <span className="task-status-text">
            {/* Show which tool the agent is calling once the model has selected one. */}
            {execution.toolName ? `Calling ${execution.toolName}…` : 'Agent analyzing…'}
          </span>
        </div>
      )}

      {execution?.status === 'Completed' && (
        <div className="task-result">
          <div className="task-status-row">
            <span className="status-icon status-success">✓</span>
            <span className="task-tool-name">{execution.toolName}</span>
            {execution.completedAt && (
              <span className="task-timestamp">{formatTime(execution.completedAt)}</span>
            )}
          </div>
          {(execution.promptTokens != null || execution.completionTokens != null) && (
            <div className="task-token-row">
              <span className="task-token-count">
                {execution.promptTokens ?? 0}↑ &nbsp;{execution.completionTokens ?? 0}↓
                &nbsp;=&nbsp;{(execution.promptTokens ?? 0) + (execution.completionTokens ?? 0)} tokens
              </span>
            </div>
          )}
          {execution.details && (
            <>
              <button
                className="task-details-toggle"
                onClick={() => setShowDetails(v => !v)}
              >
                {showDetails ? '▲ Hide details' : '▼ Show details'}
              </button>
              {showDetails && (
                <pre className="task-details-panel">{prettyJson(execution.details)}</pre>
              )}
            </>
          )}
        </div>
      )}

      {execution?.status === 'Failed' && (
        <div className="task-status-row">
          <span className="status-icon status-error">✕</span>
          <span className="task-status-text task-error-text">{execution.message}</span>
        </div>
      )}
    </div>
  );
}

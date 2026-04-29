// ============================================================
// TaskCard.tsx — Displays a single extracted HealthcareTask
//
// Each card shows:
//   - A colored badge indicating the task type
//   - The patient's full name (or "Unknown Patient" if blank)
//   - The plain-English description of what needs to happen
//
// Color-coded badges make it easy to scan a list of mixed task
// types at a glance. The colors follow a rough semantic convention:
//   Green  → medication management (refills)
//   Blue   → new orders (medications)
//   Orange → referrals (routing to another provider)
//   Purple → lab work (diagnostic)
// ============================================================

import type { HealthcareTask, TaskType } from '../types/healthcare';

// Map each TaskType to a badge background color.
// Using a Record<TaskType, string> ensures TypeScript will error if
// a new TaskType is added to the union without a corresponding color.
const BADGE_COLORS: Record<TaskType, string> = {
  MedicationRefill: '#2e7d32', // dark green
  MedicationOrder:  '#1565c0', // dark blue
  ReferralOrder:    '#e65100', // deep orange
  LabOrder:         '#6a1b9a', // deep purple
};

// Human-readable labels for the badge text.
// The enum values are PascalCase; these add spaces for readability.
const LABEL: Record<TaskType, string> = {
  MedicationRefill: 'Medication Refill',
  MedicationOrder:  'Medication Order',
  ReferralOrder:    'Referral Order',
  LabOrder:         'Lab Order',
};

export function TaskCard({ task }: { task: HealthcareTask }) {
  // Combine first and last name, filtering out empty strings so we
  // don't end up with a leading or trailing space if one is blank.
  // Fall back to "Unknown Patient" if neither name was captured.
  const patientName =
    [task.patientFirstName, task.patientLastName].filter(Boolean).join(' ') || 'Unknown Patient';

  return (
    <div className="task-card">
      {/* Colored badge identifies the task type at a glance. */}
      <span className="task-badge" style={{ backgroundColor: BADGE_COLORS[task.type] }}>
        {LABEL[task.type]}
      </span>

      {/* Patient name is the most prominent text on the card. */}
      <h3 className="task-patient">{patientName}</h3>

      {/* The description is GPT-4o's plain-English summary of what
          needs to happen. In the future agentic layer this text will
          be passed as an instruction to the appropriate tool. */}
      <p className="task-description">{task.description}</p>
    </div>
  );
}

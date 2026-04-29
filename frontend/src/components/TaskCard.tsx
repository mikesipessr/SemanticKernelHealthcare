import type { HealthcareTask, TaskType } from '../types/healthcare';

const BADGE_COLORS: Record<TaskType, string> = {
  MedicationRefill: '#2e7d32',
  MedicationOrder:  '#1565c0',
  ReferralOrder:    '#e65100',
  LabOrder:         '#6a1b9a',
};

const LABEL: Record<TaskType, string> = {
  MedicationRefill: 'Medication Refill',
  MedicationOrder:  'Medication Order',
  ReferralOrder:    'Referral Order',
  LabOrder:         'Lab Order',
};

export function TaskCard({ task }: { task: HealthcareTask }) {
  const patientName = [task.patientFirstName, task.patientLastName].filter(Boolean).join(' ') || 'Unknown Patient';

  return (
    <div className="task-card">
      <span className="task-badge" style={{ backgroundColor: BADGE_COLORS[task.type] }}>
        {LABEL[task.type]}
      </span>
      <h3 className="task-patient">{patientName}</h3>
      <p className="task-description">{task.description}</p>
    </div>
  );
}

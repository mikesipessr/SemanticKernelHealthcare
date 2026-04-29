export type TaskType =
  | 'MedicationRefill'
  | 'MedicationOrder'
  | 'ReferralOrder'
  | 'LabOrder';

export interface HealthcareTask {
  type: TaskType;
  patientFirstName: string;
  patientLastName: string;
  description: string;
}

export interface TranscribeResponse {
  transcription: string;
  tasks: HealthcareTask[];
}

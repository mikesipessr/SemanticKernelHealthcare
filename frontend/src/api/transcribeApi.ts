import type { TranscribeResponse } from '../types/healthcare';

export async function transcribeAudio(audioBlob: Blob): Promise<TranscribeResponse> {
  const formData = new FormData();
  // Field name 'audio' must match the IFormFile parameter name in AudioController
  const ext = audioBlob.type.startsWith('audio/mp4') ? 'mp4' : 'webm';
  formData.append('audio', audioBlob, `recording.${ext}`);

  const response = await fetch('/api/audio/transcribe', {
    method: 'POST',
    body: formData,
    // Do NOT set Content-Type — browser sets the multipart boundary automatically
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Transcription failed (${response.status}): ${text}`);
  }

  return response.json() as Promise<TranscribeResponse>;
}

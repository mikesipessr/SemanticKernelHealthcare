// ============================================================
// transcribeApi.ts — HTTP client for the transcription endpoint
//
// This module isolates all network communication in one place.
// Components and hooks never call fetch() directly — they import
// this function instead. That makes it easy to swap the
// implementation (e.g. add retry logic, change the base URL, or
// add an auth header) without touching UI code.
// ============================================================

import type { TranscribeResponse } from '../types/healthcare';

/**
 * Sends the recorded audio blob to the backend for transcription
 * and task classification, then returns the combined result.
 *
 * @param audioBlob - The Blob produced by MediaRecorder. The MIME
 *   type embedded in the Blob (e.g. "audio/webm;codecs=opus") is
 *   preserved so the server can tell Whisper which codec to use.
 */
export async function transcribeAudio(audioBlob: Blob): Promise<TranscribeResponse> {
  // FormData is the standard way to send files over HTTP in the
  // browser. The server reads the file via IFormFile in C#.
  const formData = new FormData();

  // Derive the file extension from the MIME type. The filename we
  // pass here ends up as IFormFile.FileName on the server side and
  // is forwarded to the TranscriptionService, which uses the
  // extension to build the filename argument for Whisper.
  // Chrome/Edge/Firefox → audio/webm → recording.webm
  // Safari             → audio/mp4  → recording.mp4
  const ext = audioBlob.type.startsWith('audio/mp4') ? 'mp4' : 'webm';
  formData.append('audio', audioBlob, `recording.${ext}`);
  // IMPORTANT: The field name 'audio' must exactly match the
  // IFormFile parameter name in AudioController.Transcribe().
  // ASP.NET Core's model binding uses the parameter name to locate
  // the correct part of the multipart form.

  const response = await fetch('/api/audio/transcribe', {
    method: 'POST',
    body: formData,
    // Do NOT set the Content-Type header manually. When you pass a
    // FormData object as the body, the browser sets Content-Type to
    // "multipart/form-data; boundary=<generated-boundary>" automatically.
    // If we set it ourselves we'd omit the boundary, and the server
    // would be unable to parse the request parts.
  });

  // Throw on non-2xx responses so callers can catch and display the
  // error. We read the response body as text first so we can include
  // any server error message in the thrown error.
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Transcription failed (${response.status}): ${text}`);
  }

  // The server returns a JSON body matching TranscribeResponse.
  // The cast here is safe because the server enforces the schema.
  return response.json() as Promise<TranscribeResponse>;
}

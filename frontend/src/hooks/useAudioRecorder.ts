// ============================================================
// useAudioRecorder.ts — Custom hook for microphone recording
//
// This hook encapsulates the entire browser audio recording
// pipeline using two Web APIs:
//
//   MediaDevices (navigator.mediaDevices)
//   — Requests microphone access and enumerates input devices.
//
//   MediaRecorder
//   — Captures the microphone stream and produces audio blobs.
//     Outputs audio/webm;codecs=opus on Chrome/Edge/Firefox or
//     audio/mp4 on Safari. Both formats are accepted by Whisper.
//
//   Web Audio API (AudioContext, AnalyserNode)
//   — Connects the microphone stream to an AnalyserNode, which
//     exposes real-time frequency data for the level meter.
//     The analyser is NOT connected to AudioContext.destination,
//     so no audio plays back through the speakers.
//
// State machine:
//   idle  →  startRecording()  →  recording
//   recording  →  stopRecording()  →  processing
//   processing  →  setIdle()  →  idle   (called by App after API returns)
// ============================================================

import { useEffect, useRef, useState } from 'react';

/** The three phases of the recorder lifecycle. */
export type RecorderState = 'idle' | 'recording' | 'processing';

export interface UseAudioRecorderReturn {
  /** Current phase of the recording lifecycle. */
  state: RecorderState;
  /**
   * Live AnalyserNode connected to the microphone stream.
   * Non-null only while recording. Pass this to useLevelMeter
   * to drive the visual level indicator.
   */
  analyserNode: AnalyserNode | null;
  /** All available audio input devices discovered after mic permission grant. */
  devices: MediaDeviceInfo[];
  /** deviceId of the currently selected microphone. */
  selectedDeviceId: string;
  setSelectedDeviceId: (id: string) => void;
  /** Opens the microphone and starts capturing audio. */
  startRecording: () => Promise<void>;
  /**
   * Stops capturing and returns the complete audio as a Blob.
   * Also transitions state to 'processing' and clears the analyserNode.
   */
  stopRecording: () => Promise<Blob>;
  /** Resets state back to 'idle'. Called by App after the API responds. */
  setIdle: () => void;
  /** Set when microphone permission is denied or another setup error occurs. */
  error: string | null;
}

export function useAudioRecorder(): UseAudioRecorderReturn {
  const [state, setState]               = useState<RecorderState>('idle');
  const [analyserNode, setAnalyserNode] = useState<AnalyserNode | null>(null);
  const [devices, setDevices]           = useState<MediaDeviceInfo[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string>('');
  const [error, setError]               = useState<string | null>(null);

  // Refs hold mutable values that don't trigger re-renders.
  // We use refs for the "infrastructure" objects that span the
  // start/stop calls, and state only for values the UI needs to react to.
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const streamRef        = useRef<MediaStream | null>(null);
  const audioContextRef  = useRef<AudioContext | null>(null);
  const chunksRef        = useRef<Blob[]>([]);  // accumulates MediaRecorder data events

  // ----------------------------------------------------------
  // On mount: request mic permission and enumerate devices.
  //
  // Browsers only reveal microphone device labels after the user
  // has granted permission to at least one audio device. So we
  // request a temporary stream, immediately stop it (we just
  // needed the permission grant), then enumerate devices to get
  // the human-readable labels for the dropdown.
  // ----------------------------------------------------------
  useEffect(() => {
    navigator.mediaDevices
      .getUserMedia({ audio: true })
      .then((stream) => {
        // Stop immediately — we only needed the permission prompt.
        stream.getTracks().forEach((t) => t.stop());
        return navigator.mediaDevices.enumerateDevices();
      })
      .then((all) => {
        const inputs = all.filter((d) => d.kind === 'audioinput');
        setDevices(inputs);
        // Default to the first available device.
        if (inputs.length > 0) setSelectedDeviceId(inputs[0].deviceId);
      })
      .catch(() => setError('Microphone access denied. Please allow mic access and refresh.'));
  }, []);

  // ----------------------------------------------------------
  // startRecording — opens the selected microphone and wires up
  // the MediaRecorder and Web Audio analyser.
  // ----------------------------------------------------------
  const startRecording = async (): Promise<void> => {
    setError(null);

    // Ask for exactly the selected device if one was chosen,
    // otherwise accept any available microphone.
    const constraints: MediaStreamConstraints = {
      audio: selectedDeviceId ? { deviceId: { exact: selectedDeviceId } } : true,
    };

    const stream = await navigator.mediaDevices.getUserMedia(constraints);
    streamRef.current = stream;

    // Build the Web Audio pipeline for the level meter.
    // AudioContext is the entry point to the Web Audio graph.
    const audioContext = new AudioContext();
    audioContextRef.current = audioContext;

    // createMediaStreamSource wraps the live microphone stream
    // as a Web Audio source node.
    const source = audioContext.createMediaStreamSource(stream);

    // The AnalyserNode performs an FFT (Fast Fourier Transform) on
    // the audio data. fftSize controls the frequency resolution;
    // 256 gives 128 frequency bins — enough for a smooth level meter
    // without being wasteful.
    const analyser = audioContext.createAnalyser();
    analyser.fftSize = 256;

    // Connect source → analyser. We intentionally do NOT connect the
    // analyser to audioContext.destination, so the mic audio is never
    // played back through speakers (which would cause feedback).
    source.connect(analyser);
    setAnalyserNode(analyser); // expose to useLevelMeter via state

    // Pick the best supported audio format. We try in preference order:
    //   audio/webm;codecs=opus  — best quality, widely supported (Chrome/Edge/Firefox)
    //   audio/webm              — fallback without explicit codec
    //   audio/mp4               — Safari's format (AAC codec)
    // All three are accepted directly by OpenAI's Whisper endpoint.
    const mimeType = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4'].find((t) =>
      MediaRecorder.isTypeSupported(t)
    );

    chunksRef.current = [];
    const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);

    // ondataavailable fires every 100 ms (see recorder.start(100) below)
    // delivering a Blob chunk of audio data. We accumulate the chunks
    // and assemble the final Blob when recording stops.
    recorder.ondataavailable = (e) => {
      if (e.data.size > 0) chunksRef.current.push(e.data);
    };

    mediaRecorderRef.current = recorder;

    // The timeslice argument (100 ms) makes ondataavailable fire
    // periodically during recording rather than only at the end.
    // This ensures we collect data even if something goes wrong before stop().
    recorder.start(100);
    setState('recording');
  };

  // ----------------------------------------------------------
  // stopRecording — stops the recorder and returns the audio Blob.
  //
  // MediaRecorder.stop() is asynchronous: it schedules the stop
  // and fires onstop after flushing any buffered data. We wrap
  // this in a Promise so callers can await the complete Blob.
  // ----------------------------------------------------------
  const stopRecording = (): Promise<Blob> =>
    new Promise((resolve) => {
      const recorder = mediaRecorderRef.current!;

      // onstop fires after the recorder has flushed all buffered chunks.
      // At this point chunksRef.current contains all the audio data.
      recorder.onstop = () => {
        resolve(new Blob(chunksRef.current, { type: recorder.mimeType }));
      };

      recorder.stop();

      // Release the microphone immediately so the browser stops
      // showing the "mic in use" indicator in the tab.
      streamRef.current?.getTracks().forEach((t) => t.stop());
      streamRef.current = null;

      // Tear down the Web Audio graph. close() releases the audio
      // hardware and any associated resources.
      audioContextRef.current?.close();
      audioContextRef.current = null;

      // Nulling the analyser node stops the useLevelMeter animation loop
      // (the hook watches for null and cancels its requestAnimationFrame).
      setAnalyserNode(null);

      setState('processing');
    });

  return {
    state,
    analyserNode,
    devices,
    selectedDeviceId,
    setSelectedDeviceId,
    startRecording,
    stopRecording,
    // setIdle is called by App in the finally block of handleStop,
    // returning to idle whether the API call succeeded or failed.
    setIdle: () => setState('idle'),
    error,
  };
}

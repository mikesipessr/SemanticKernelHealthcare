import { useEffect, useRef, useState } from 'react';

export type RecorderState = 'idle' | 'recording' | 'processing';

export interface UseAudioRecorderReturn {
  state: RecorderState;
  analyserNode: AnalyserNode | null;
  devices: MediaDeviceInfo[];
  selectedDeviceId: string;
  setSelectedDeviceId: (id: string) => void;
  startRecording: () => Promise<void>;
  stopRecording: () => Promise<Blob>;
  error: string | null;
}

export function useAudioRecorder(): UseAudioRecorderReturn {
  const [state, setState] = useState<RecorderState>('idle');
  const [analyserNode, setAnalyserNode] = useState<AnalyserNode | null>(null);
  const [devices, setDevices] = useState<MediaDeviceInfo[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const chunksRef = useRef<Blob[]>([]);

  useEffect(() => {
    // Request mic permission once on mount, then enumerate devices to get labels
    navigator.mediaDevices
      .getUserMedia({ audio: true })
      .then((stream) => {
        stream.getTracks().forEach((t) => t.stop());
        return navigator.mediaDevices.enumerateDevices();
      })
      .then((all) => {
        const inputs = all.filter((d) => d.kind === 'audioinput');
        setDevices(inputs);
        if (inputs.length > 0) setSelectedDeviceId(inputs[0].deviceId);
      })
      .catch(() => setError('Microphone access denied. Please allow mic access and refresh.'));
  }, []);

  const startRecording = async (): Promise<void> => {
    setError(null);
    const constraints: MediaStreamConstraints = {
      audio: selectedDeviceId ? { deviceId: { exact: selectedDeviceId } } : true,
    };

    const stream = await navigator.mediaDevices.getUserMedia(constraints);
    streamRef.current = stream;

    const audioContext = new AudioContext();
    audioContextRef.current = audioContext;
    const source = audioContext.createMediaStreamSource(stream);
    const analyser = audioContext.createAnalyser();
    analyser.fftSize = 256;
    source.connect(analyser);
    setAnalyserNode(analyser);

    const mimeType = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4'].find((t) =>
      MediaRecorder.isTypeSupported(t)
    );

    chunksRef.current = [];
    const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
    recorder.ondataavailable = (e) => {
      if (e.data.size > 0) chunksRef.current.push(e.data);
    };
    mediaRecorderRef.current = recorder;
    recorder.start(100);
    setState('recording');
  };

  const stopRecording = (): Promise<Blob> =>
    new Promise((resolve) => {
      const recorder = mediaRecorderRef.current!;
      recorder.onstop = () => {
        resolve(new Blob(chunksRef.current, { type: recorder.mimeType }));
      };
      recorder.stop();

      streamRef.current?.getTracks().forEach((t) => t.stop());
      streamRef.current = null;

      audioContextRef.current?.close();
      audioContextRef.current = null;
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
    error,
  };
}

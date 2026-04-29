import { useEffect, useRef, useState } from 'react';

export function useLevelMeter(analyserNode: AnalyserNode | null): number {
  const [level, setLevel] = useState(0);
  const animFrameRef = useRef<number>(0);

  useEffect(() => {
    if (!analyserNode) {
      setLevel(0);
      return;
    }

    const dataArray = new Uint8Array(analyserNode.frequencyBinCount);

    const tick = () => {
      analyserNode.getByteFrequencyData(dataArray);
      const avg = dataArray.reduce((sum, val) => sum + val, 0) / dataArray.length;
      setLevel(avg / 255);
      animFrameRef.current = requestAnimationFrame(tick);
    };

    animFrameRef.current = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(animFrameRef.current);
  }, [analyserNode]);

  return level;
}

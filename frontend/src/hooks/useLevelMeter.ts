// ============================================================
// useLevelMeter.ts — Animates an audio level reading from an AnalyserNode
//
// The Web Audio API's AnalyserNode performs a real-time FFT on the
// audio stream and exposes the results via getByteFrequencyData().
// This hook reads that data on every animation frame and converts
// it to a single 0..1 "level" value that the LevelMeter component
// uses to determine how many bars to light up.
//
// Design choices:
//   - requestAnimationFrame keeps the animation synced to the
//     display refresh rate (~60 fps) and automatically pauses when
//     the tab is hidden, saving CPU.
//   - We cancel the animation frame in the useEffect cleanup,
//     so the loop stops immediately when analyserNode becomes null
//     (i.e. when recording stops).
//   - Using average frequency magnitude rather than RMS amplitude
//     is a simplification that still produces a responsive meter
//     for speech-frequency content.
// ============================================================

import { useEffect, useRef, useState } from 'react';

/**
 * Reads real-time audio level data from an AnalyserNode and
 * returns a normalized 0..1 level value, updated every frame.
 *
 * @param analyserNode - Active AnalyserNode from useAudioRecorder,
 *   or null when not recording. When null the hook returns 0 and
 *   stops the animation loop.
 */
export function useLevelMeter(analyserNode: AnalyserNode | null): number {
  const [level, setLevel] = useState(0);

  // Store the animation frame ID in a ref (not state) so we can
  // cancel it without causing re-renders.
  const animFrameRef = useRef<number>(0);

  useEffect(() => {
    // When the analyser is gone (recording stopped), reset to zero
    // and skip starting the loop — the cleanup function below will
    // cancel any in-flight frame from the previous render.
    if (!analyserNode) {
      setLevel(0);
      return;
    }

    // frequencyBinCount = fftSize / 2 (128 bins for fftSize 256).
    // Each bin holds the magnitude of a frequency range as a value
    // from 0 (silent) to 255 (maximum amplitude).
    const dataArray = new Uint8Array(analyserNode.frequencyBinCount);

    const tick = () => {
      // Populate dataArray with the latest frequency magnitudes.
      analyserNode.getByteFrequencyData(dataArray);

      // Average all frequency bins into one value, then normalize
      // to 0..1 by dividing by 255 (the maximum byte value).
      // Speech energy concentrates in lower-frequency bins, so the
      // average naturally rises when someone is talking.
      const avg = dataArray.reduce((sum, val) => sum + val, 0) / dataArray.length;
      setLevel(avg / 255);

      // Schedule the next frame. We store the ID so we can cancel it.
      animFrameRef.current = requestAnimationFrame(tick);
    };

    // Kick off the loop.
    animFrameRef.current = requestAnimationFrame(tick);

    // Cleanup: cancel the pending frame when analyserNode changes
    // (which happens when recording stops and the node is set to null).
    return () => cancelAnimationFrame(animFrameRef.current);
  }, [analyserNode]); // Re-run whenever the analyser changes

  return level;
}

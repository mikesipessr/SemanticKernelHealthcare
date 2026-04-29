// ============================================================
// LevelMeter.tsx — Real-time audio level visualization
//
// Renders a row of 20 vertical bars that light up left-to-right
// based on the current audio level (0..1) supplied by useLevelMeter.
//
// Color mapping: bar index 0 (leftmost) is pure green (hsl 120),
// and each successive bar shifts 6 degrees toward red (hsl 0),
// giving a smooth green → yellow → red gradient that matches the
// convention on hardware VU meters.
//
// When `active` is false (not recording) all bars remain grey —
// the color logic only applies during active recording.
// ============================================================

interface LevelMeterProps {
  /** Normalized audio level from useLevelMeter, range 0..1. */
  level: number;
  /** True only while recording. False → all bars stay grey. */
  active: boolean;
}

export function LevelMeter({ level, active }: LevelMeterProps) {
  const bars = 20;

  // Convert the 0..1 level to a bar count. Math.round gives a clean
  // step rather than a fractional "half lit" bar.
  const filled = Math.round(level * bars);

  return (
    <div className="level-meter" aria-label={`Audio level: ${Math.round(level * 100)}%`}>
      {Array.from({ length: bars }, (_, i) => {
        // A bar is "active" (colored) only when the recorder is running
        // AND the current level reaches this bar's position.
        const isActive = active && i < filled;

        // HSL hue: bar 0 = 120 (green), bar 19 = 120 - 19*6 = 6 (near red).
        // Saturation 80% and lightness 45% give vivid, readable colors.
        const hue = 120 - i * 6;

        return (
          <div
            key={i}
            className="level-bar"
            style={{ backgroundColor: isActive ? `hsl(${hue}, 80%, 45%)` : undefined }}
            data-active={isActive}
          />
        );
      })}
    </div>
  );
}

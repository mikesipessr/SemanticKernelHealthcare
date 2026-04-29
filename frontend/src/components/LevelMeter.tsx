interface LevelMeterProps {
  level: number; // 0..1
  active: boolean;
}

export function LevelMeter({ level, active }: LevelMeterProps) {
  const bars = 20;
  const filled = Math.round(level * bars);

  return (
    <div className="level-meter" aria-label={`Audio level: ${Math.round(level * 100)}%`}>
      {Array.from({ length: bars }, (_, i) => {
        const isActive = active && i < filled;
        const hue = 120 - i * 6; // green → yellow → red
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

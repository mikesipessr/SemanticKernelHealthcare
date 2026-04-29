// ============================================================
// DeviceSelector.tsx — Microphone device picker
//
// Renders a labeled <select> populated with the audio input devices
// discovered by useAudioRecorder. Disabled during recording and
// processing so the user can't switch devices mid-capture.
//
// If no devices are available yet (permission not granted, or the
// enumeration is still in progress) the component renders nothing.
// This avoids a flash of an empty dropdown on load.
// ============================================================

interface DeviceSelectorProps {
  /** Audio input devices returned by navigator.mediaDevices.enumerateDevices(). */
  devices: MediaDeviceInfo[];
  /** deviceId of the currently selected device. */
  selectedDeviceId: string;
  /** Called when the user picks a different device. */
  onChange: (deviceId: string) => void;
  /** True while recording or processing — prevents mid-session device changes. */
  disabled: boolean;
}

export function DeviceSelector({ devices, selectedDeviceId, onChange, disabled }: DeviceSelectorProps) {
  // Don't render until we have at least one device to show.
  // Device enumeration happens asynchronously after mic permission,
  // so this avoids an empty dropdown flash on initial load.
  if (devices.length === 0) return null;

  return (
    <div className="device-selector">
      <label htmlFor="device-select">Microphone</label>
      <select
        id="device-select"
        value={selectedDeviceId}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
      >
        {devices.map((d) => (
          <option key={d.deviceId} value={d.deviceId}>
            {/*
              d.label is the human-readable name (e.g. "Headset Microphone").
              It's empty until mic permission has been granted, so we fall
              back to a truncated deviceId as a placeholder. After the first
              permission grant the real labels appear.
            */}
            {d.label || `Microphone ${d.deviceId.slice(0, 8)}`}
          </option>
        ))}
      </select>
    </div>
  );
}

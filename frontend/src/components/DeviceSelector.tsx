interface DeviceSelectorProps {
  devices: MediaDeviceInfo[];
  selectedDeviceId: string;
  onChange: (deviceId: string) => void;
  disabled: boolean;
}

export function DeviceSelector({ devices, selectedDeviceId, onChange, disabled }: DeviceSelectorProps) {
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
            {d.label || `Microphone ${d.deviceId.slice(0, 8)}`}
          </option>
        ))}
      </select>
    </div>
  );
}

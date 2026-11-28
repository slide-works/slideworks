import * as Device from 'expo-device';

// What the presenter window's connected list calls this phone: the name its owner gave it, else its model.
export function deviceLabel(): string {
  return Device.deviceName ?? Device.modelName ?? 'Phone';
}

export function deviceTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

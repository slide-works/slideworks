import { api } from './client';
import type { MeView } from './me';

// The device-session HTTP API (contract K4). The server words every refusal's message; clients show it as given.

export const DEVICE_SESSIONS_PATH = '/api/auth/device-sessions';

// One signed-in phone. Labels are pre-formatted by the server in the person's time zone. current is true only for the
// bearer caller's own session.
export type DeviceRow = {
  id: string;
  deviceName: string;
  signedInLabel: string;
  lastUsedLabel: string;
  current: boolean;
};

// GET /api/auth/device-sessions (device-sessions.json): newest first, ended sessions left out.
export type DeviceSessionsView = { devices: DeviceRow[] };

// POST /api/auth/device-sessions/codes. challenge is base64url(SHA-256(verifier)), 43 characters; state is 16–128
// base64url characters; deviceName is 1–80 characters after trim.
export type DeviceSessionCodeRequest = { challenge: string; state: string; deviceName: string };

// device-session-code.json: slideworks://signed-in?code=…&state=…&iss=<public origin>.
export type DeviceSessionCodeView = { openAppUrl: string };

// POST /api/auth/device-sessions/exchange. verifier is 43–128 characters.
export type DeviceSessionExchangeRequest = { code: string; verifier: string };

// device-session-exchange.json: the new session, its swd_ secret (shown once) and GET /api/me's view.
export type DeviceSessionExchangeView = { session: DeviceRow; secret: string; me: MeView };

// Cookie only: mints the one-use code the confirm page hands to the app.
export function createDeviceSessionCode(
  request: DeviceSessionCodeRequest,
): Promise<DeviceSessionCodeView> {
  return api.post<DeviceSessionCodeView>(`${DEVICE_SESSIONS_PATH}/codes`, request);
}

// Anonymous: trades the code and its PKCE verifier for a session and its secret.
export function exchangeDeviceSessionCode(
  request: DeviceSessionExchangeRequest,
): Promise<DeviceSessionExchangeView> {
  return api.post<DeviceSessionExchangeView>(`${DEVICE_SESSIONS_PATH}/exchange`, request);
}

export function listDeviceSessions(): Promise<DeviceSessionsView> {
  return api.get<DeviceSessionsView>(DEVICE_SESSIONS_PATH);
}

// Ends one of the person's sessions.
export function revokeDeviceSession(id: string): Promise<void> {
  return api.del<void>(`${DEVICE_SESSIONS_PATH}/${encodeURIComponent(id)}`);
}

// Bearer only: the app signs itself out.
export function endCurrentDeviceSession(): Promise<void> {
  return api.del<void>(`${DEVICE_SESSIONS_PATH}/current`);
}

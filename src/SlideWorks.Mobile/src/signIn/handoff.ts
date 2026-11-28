// The app side of the sign-in handoff (contract K6): PKCE with an issuer check (RFC 9207). Pure: randomness and SHA-256
// come in as functions, so this runs under Vitest without React Native.

// Where the confirm page sends the browser back to; openAuthSessionAsync waits for it.
export const SIGN_IN_REDIRECT = 'slideworks://signed-in';

// expo-device's names; the first non-empty one names the phone on the confirm page and in Signed-in devices.
export type DeviceNames = { deviceName?: string | null; modelName?: string | null };

const DEVICE_NAME_MAX = 80;
const VERIFIER_BYTES = 32; // 43 base64url characters
const STATE_BYTES = 24; // 32 base64url characters

const BASE64URL = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';

// base64url without padding (RFC 4648 §5).
export function base64Url(bytes: Uint8Array): string {
  let out = '';
  for (let i = 0; i < bytes.length; i += 3) {
    const a = bytes[i];
    const b = i + 1 < bytes.length ? bytes[i + 1] : 0;
    const c = i + 2 < bytes.length ? bytes[i + 2] : 0;
    const n = (a << 16) | (b << 8) | c;
    out += BASE64URL[(n >> 18) & 63] + BASE64URL[(n >> 12) & 63];
    if (i + 1 < bytes.length) out += BASE64URL[(n >> 6) & 63];
    if (i + 2 < bytes.length) out += BASE64URL[n & 63];
  }
  return out;
}

// Standard base64 (what expo-crypto's digest returns) to base64url without padding.
export function base64ToBase64Url(base64: string): string {
  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function newVerifier(randomBytes: (count: number) => Uint8Array): string {
  return base64Url(randomBytes(VERIFIER_BYTES));
}

export function newState(randomBytes: (count: number) => Uint8Array): string {
  return base64Url(randomBytes(STATE_BYTES));
}

// challenge = base64url(SHA-256(ASCII(verifier))), no padding. sha256Base64 returns the digest as standard base64.
export async function challengeFor(
  verifier: string,
  sha256Base64: (data: string) => Promise<string>,
): Promise<string> {
  return base64ToBase64Url(await sha256Base64(verifier));
}

export function deviceNameFrom(names: DeviceNames): string {
  for (const name of [names.deviceName, names.modelName]) {
    const trimmed = name?.trim();
    if (trimmed) return trimmed.slice(0, DEVICE_NAME_MAX).trim();
  }
  return 'Phone';
}

// Lower-case scheme and host, no path or trailing slash, default port dropped. An address typed without a scheme is
// https. Anything that isn't an http(s) address with a host is null.
export function normaliseOrigin(input: string): string | null {
  const typed = input.trim();
  if (!typed) return null;
  const withScheme = /^[a-z][a-z0-9+.-]*:\/\//i.test(typed) ? typed : `https://${typed}`;
  try {
    const url = new URL(withScheme);
    if ((url.protocol !== 'https:' && url.protocol !== 'http:') || !url.hostname) return null;
    return url.origin.toLowerCase();
  } catch {
    return null;
  }
}

export type SignInRequest = { challenge: string; state: string; deviceName: string };

// The confirm page the system browser opens.
export function signInUrl(origin: string, request: SignInRequest): string {
  const query = new URLSearchParams({
    challenge: request.challenge,
    state: request.state,
    device: request.deviceName,
  });
  return `${origin}/app/sign-in?${query.toString()}`;
}

export type Redirect =
  // Trade the code for a session.
  | { kind: 'code'; code: string }
  // The person tapped Cancel on the confirm page: back to Sign in, quietly.
  | { kind: 'cancelled' }
  // Wrong state or iss: it didn't come from the server the person typed, so never exchange.
  | { kind: 'foreign' }
  // From the right server but without a code or a known error.
  | { kind: 'incomplete' };

// Accepts the browser's redirect only when its state is the one this sign-in made and its iss is the typed origin.
export function parseRedirect(url: string, expected: { state: string; origin: string }): Redirect {
  if (!url.startsWith(SIGN_IN_REDIRECT)) return { kind: 'foreign' };
  const rest = url.slice(SIGN_IN_REDIRECT.length);
  if (rest !== '' && !rest.startsWith('?') && !rest.startsWith('/?')) return { kind: 'foreign' };
  const query = new URLSearchParams(rest.slice(rest.indexOf('?') + 1));
  const iss = normaliseOrigin(query.get('iss') ?? '');
  const origin = normaliseOrigin(expected.origin);
  if (query.get('state') !== expected.state || iss === null || iss !== origin) {
    return { kind: 'foreign' };
  }
  if (query.get('error') === 'cancelled') return { kind: 'cancelled' };
  const code = query.get('code');
  return code ? { kind: 'code', code } : { kind: 'incomplete' };
}

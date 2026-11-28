import { createHash } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import {
  base64Url,
  challengeFor,
  deviceNameFrom,
  newState,
  newVerifier,
  normaliseOrigin,
  parseRedirect,
  signInUrl,
} from './handoff';

// RFC 7636 Appendix B.
const rfcVerifierBytes = Uint8Array.from([
  116, 24, 223, 180, 151, 153, 224, 37, 79, 250, 96, 125, 216, 173, 187, 186, 22, 212, 37, 77, 105,
  214, 191, 240, 91, 88, 5, 88, 83, 132, 141, 121,
]);
const rfcVerifier = 'dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk';
const rfcChallenge = 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM';

const sha256Base64 = async (data: string) => createHash('sha256').update(data).digest('base64');
const counting = (count: number) => Uint8Array.from({ length: count }, (_, i) => (i * 37) & 255);

const origin = 'https://slideworks.example';
const state = 'state-0123456789abcdef';
const redirect = (query: Record<string, string>) =>
  `slideworks://signed-in?${new URLSearchParams(query).toString()}`;

describe('handoff', () => {
  it('challenge is base64url sha256 of verifier', async () => {
    expect(base64Url(rfcVerifierBytes)).toBe(rfcVerifier);
    expect(await challengeFor(rfcVerifier, sha256Base64)).toBe(rfcChallenge);
  });

  it('makes a 43-character verifier and a state the confirm page accepts', () => {
    expect(newVerifier(counting)).toMatch(/^[A-Za-z0-9_-]{43}$/);
    expect(newState(counting)).toMatch(/^[A-Za-z0-9_-]{16,128}$/);
  });

  it('opens the confirm page on the typed origin with challenge, state and device', () => {
    const url = new URL(
      signInUrl(origin, { challenge: rfcChallenge, state, deviceName: "Sam's phone" }),
    );

    expect(url.origin + url.pathname).toBe(`${origin}/app/sign-in`);
    expect(Object.fromEntries(url.searchParams)).toEqual({
      challenge: rfcChallenge,
      state,
      device: "Sam's phone",
    });
  });

  it('names the device from expo-device, trimmed to 80 characters', () => {
    expect(deviceNameFrom({ deviceName: '  Sam  ', modelName: 'Pixel' })).toBe('Sam');
    expect(deviceNameFrom({ deviceName: ' ', modelName: 'Pixel 9' })).toBe('Pixel 9');
    expect(deviceNameFrom({ deviceName: null, modelName: null })).toBe('Phone');
    expect(deviceNameFrom({ deviceName: 'x'.repeat(100) })).toHaveLength(80);
  });

  it('accepts a redirect with the right state and iss', () => {
    expect(
      parseRedirect(redirect({ code: 'c0de', state, iss: origin }), { state, origin }),
    ).toEqual({ kind: 'code', code: 'c0de' });
  });

  it('refuses a redirect with wrong state', () => {
    expect(
      parseRedirect(redirect({ code: 'c0de', state: 'another-state-0000', iss: origin }), {
        state,
        origin,
      }),
    ).toEqual({ kind: 'foreign' });
    expect(parseRedirect(redirect({ code: 'c0de', iss: origin }), { state, origin })).toEqual({
      kind: 'foreign',
    });
  });

  it('refuses a redirect from another iss', () => {
    expect(
      parseRedirect(redirect({ code: 'c0de', state, iss: 'https://evil.example' }), {
        state,
        origin,
      }),
    ).toEqual({ kind: 'foreign' });
    expect(parseRedirect(redirect({ code: 'c0de', state }), { state, origin })).toEqual({
      kind: 'foreign',
    });
    expect(
      parseRedirect(`slideworks://join/123456?code=c0de&state=${state}&iss=${origin}`, {
        state,
        origin,
      }),
    ).toEqual({ kind: 'foreign' });
  });

  it('normalises origins before comparing', () => {
    expect(normaliseOrigin('HTTPS://SlideWorks.Example:443/')).toBe(origin);
    expect(normaliseOrigin('slideworks.example')).toBe(origin);
    expect(normaliseOrigin(' http://10.0.2.2:5510/path ')).toBe('http://10.0.2.2:5510');
    expect(normaliseOrigin('http://localhost:80')).toBe('http://localhost');
    expect(normaliseOrigin('ftp://slideworks.example')).toBeNull();
    expect(normaliseOrigin('')).toBeNull();

    expect(
      parseRedirect(redirect({ code: 'c0de', state, iss: 'https://SLIDEWORKS.example:443/' }), {
        state,
        origin: 'https://slideworks.example/',
      }),
    ).toEqual({ kind: 'code', code: 'c0de' });
  });

  it('reads error=cancelled', () => {
    expect(
      parseRedirect(redirect({ error: 'cancelled', state, iss: origin }), { state, origin }),
    ).toEqual({ kind: 'cancelled' });
    expect(
      parseRedirect(redirect({ error: 'cancelled', state, iss: 'https://evil.example' }), {
        state,
        origin,
      }),
    ).toEqual({ kind: 'foreign' });
  });

  it('a matching redirect without a code is incomplete', () => {
    expect(parseRedirect(redirect({ state, iss: origin }), { state, origin })).toEqual({
      kind: 'incomplete',
    });
  });
});

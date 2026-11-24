import { describe, expect, it } from 'vitest';
import { deviceLabel } from './deviceLabel';

describe('deviceLabel', () => {
  it.each([
    [
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1',
      'Safari on iPhone',
    ],
    [
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1',
      'Chrome on iPhone',
    ],
    [
      'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36',
      'Chrome on Android',
    ],
    [
      'Mozilla/5.0 (Linux; Android 15; SM-S921B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/27.0 Chrome/125.0 Mobile Safari/537.36',
      'Samsung Internet on Android',
    ],
    ['Mozilla/5.0 (Android 15; Mobile; rv:132.0) Gecko/132.0 Firefox/132.0', 'Firefox on Android'],
    [
      'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 Edg/130.0',
      'Edge on Windows',
    ],
    [
      'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36 OPR/80.0',
      'Opera on Android',
    ],
    [
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) OPiOS/16.0 Mobile/15E148 Safari/9537.53',
      'Opera on iPhone',
    ],
    [
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 EdgiOS/130.0 Mobile/15E148 Safari/605.1.15',
      'Edge on iPhone',
    ],
    [
      'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36 EdgA/130.0',
      'Edge on Android',
    ],
    [
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) FxiOS/132.0 Mobile/15E148 Safari/605.1.15',
      'Firefox on iPhone',
    ],
    [
      'Mozilla/5.0 (Macintosh; Intel Mac OS 14_0) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15',
      'Safari on Mac',
    ],
    ['Mozilla/5.0 (X11; Linux x86_64)', 'Linux'],
    ['Safari/605.1.15', 'Safari'],
  ])('labels %s as %s', (ua, label) => {
    expect(deviceLabel(ua, 'Phone')).toBe(label);
  });

  it.each([
    ['curl/8.0', 'Phone'],
    ['curl/8.0', 'Browser'],
    ['', 'Browser'],
  ])('labels the unrecognised %j with the fallback %s', (ua, fallback) => {
    expect(deviceLabel(ua, fallback)).toBe(fallback);
  });
});

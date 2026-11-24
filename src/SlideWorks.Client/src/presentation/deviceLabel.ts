// The label a window joins its presentation with, as the presenter window's chips show it: "Safari on iPhone",
// "Chrome on Android". Read from the user agent once per page; when neither the browser nor the device is
// recognised the caller's fallback word is the label ("Phone" on the remote, "Browser" on a window).
export function deviceLabel(userAgent: string, fallback: string): string {
  const browser = browserOf(userAgent);
  const device = deviceOf(userAgent);
  if (browser && device) return `${browser} on ${device}`;
  return browser ?? device ?? fallback;
}

function browserOf(ua: string): string | null {
  // Order matters: Edge and Opera say "Chrome" too, every iOS browser says "Safari", and Chrome says "Safari".
  if (/EdgiOS|EdgA|Edg\//.test(ua)) return 'Edge';
  if (/OPR\/|OPiOS/.test(ua)) return 'Opera';
  if (/SamsungBrowser/.test(ua)) return 'Samsung Internet';
  if (/Firefox|FxiOS/.test(ua)) return 'Firefox';
  if (/Chrome|CriOS/.test(ua)) return 'Chrome';
  if (/Safari/.test(ua)) return 'Safari';
  return null;
}

function deviceOf(ua: string): string | null {
  if (/iPhone/.test(ua)) return 'iPhone';
  if (/iPad/.test(ua)) return 'iPad';
  if (/Android/.test(ua)) return 'Android';
  if (/Windows/.test(ua)) return 'Windows';
  if (/Mac OS X|Macintosh/.test(ua)) return 'Mac';
  if (/Linux/.test(ua)) return 'Linux';
  return null;
}

// The live server check on Sign in: GET {origin}/health answers 2xx within 4 seconds. No credentials ride along.
export const SERVER_CHECK_TIMEOUT_MS = 4000;

export async function serverReachable(origin: string): Promise<boolean> {
  const abort = new AbortController();
  const timer = setTimeout(() => abort.abort(), SERVER_CHECK_TIMEOUT_MS);
  try {
    const response = await fetch(`${origin}/health`, { credentials: 'omit', signal: abort.signal });
    return response.ok;
  } catch {
    return false;
  } finally {
    clearTimeout(timer);
  }
}

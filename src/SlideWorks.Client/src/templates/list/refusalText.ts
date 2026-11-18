import { ApiError } from '../../api/client';

// The sentence the server wrote for a refused call: a refusal's `message`, or an `error` that is
// itself a sentence. Null for anything else (a code, no body, a dropped connection), so the caller
// shows its own fixed sentence and never a code.
export function refusalText(error: unknown): string | null {
  if (!(error instanceof ApiError)) return null;
  if (error.detail) return error.detail;
  const code = error.code;
  return code !== undefined && /\s/.test(code) && !code.startsWith('Request failed') ? code : null;
}

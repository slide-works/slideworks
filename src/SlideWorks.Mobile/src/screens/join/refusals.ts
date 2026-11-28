// What the join screen says when the server refuses a code (contract K7). The words are the web's, copied from
// src/SlideWorks.Client/src/guest/JoinCodePage.tsx (the ADVICE map and the no_session sentence); refusals.test.ts fails
// when they drift from it.

export type JoinRefusal = 'no_session' | 'too_many_attempts';

export const JOIN_REFUSALS: readonly JoinRefusal[] = ['no_session', 'too_many_attempts'];

export const REFUSAL_ADVICE: Record<JoinRefusal, string> = {
  no_session: 'Check the code on the screen. Did you swap two digits?',
  too_many_attempts: 'Too many tries. Wait a minute and try again.',
};

// The web's "No live presentation uses <b>123 456</b>." before the no_session advice.
export const NO_SESSION_LEAD = 'No live presentation uses ';

export function spacedCode(code: string): string {
  return `${code.slice(0, 3)} ${code.slice(3)}`;
}

export function isJoinRefusal(code: string | undefined): code is JoinRefusal {
  return JOIN_REFUSALS.includes(code as JoinRefusal);
}

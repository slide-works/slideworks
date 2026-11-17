import { JOIN_CODE_TEST_IDS } from './testIds';

// Why the digits are being asked for again: the code has no session, or too many wrong codes were tried. The page says
// what to check and keeps the digits so the guest can fix one. It never shows a 404.
export type JoinCodeError = 'no_session' | 'too_many_attempts';

// /join, the keypad: six digit boxes that go to /j/{code} on the sixth digit. GuestPage shows it too, with the code it
// could not open and the error.
export function JoinCodePage({ code, error }: { code?: string; error?: JoinCodeError }) {
  // TODO: the six boxes (numeric keypad, focus on the first, paste, backspace) prefilled with code, the error with its
  // advice and focus moved to it, and the move to /j/{code} with replace on the sixth digit.
  return (
    <main data-testid={JOIN_CODE_TEST_IDS.page}>
      <h1>Enter the code</h1>
      {error && (
        <p data-testid={JOIN_CODE_TEST_IDS.error} role="alert">
          {code ? `${code}: ` : ''}
          {error}
        </p>
      )}
    </main>
  );
}

import { useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { JOIN_CODE_LENGTH, JOIN_CODE_TEST_IDS, joinCodeBoxTestId } from './testIds';

// Why the digits are being asked for again: the code has no session, or too many wrong codes were tried. The page says
// what to check and keeps the digits so the guest can fix one. It never shows a 404.
export type JoinCodeError = 'no_session' | 'too_many_attempts';

const ADVICE: Record<JoinCodeError, string> = {
  no_session: 'Check the code on the screen. Did you swap two digits?',
  too_many_attempts: 'Too many tries. Wait a minute and try again.',
};

const positions = Array.from({ length: JOIN_CODE_LENGTH }, (_, index) => index);
const onlyDigits = (text: string) => text.replace(/\D/g, '').slice(0, JOIN_CODE_LENGTH);

// State handed to /join so that retyping the code GuestPage just rejected loads it again: GuestPage keys its read on the
// code, so the same code needs a visit to another route before /j/{code} remounts it.
type RetryState = { retry: string };

// /join, the keypad: six digit boxes that go to /j/{code} on the sixth digit. GuestPage shows it too, with the code it
// could not open and the error.
export function JoinCodePage({ code, error }: { code?: string; error?: JoinCodeError }) {
  const navigate = useNavigate();
  const location = useLocation();
  const retry = (location.state as Partial<RetryState> | null)?.retry;
  const [digits, setDigits] = useState<string[]>(() =>
    positions.map((position) => onlyDigits(retry ?? code ?? '')[position] ?? ''),
  );
  const [typed, setTyped] = useState(false);
  const [localError, setLocalError] = useState<JoinCodeError | null>(null);
  const boxes = useRef<(HTMLInputElement | null)[]>([]);
  const errorRef = useRef<HTMLParagraphElement>(null);
  const shown = localError ?? (typed ? null : (error ?? null));

  const go = (next: string[]) => {
    const value = next.join('');
    // No join code starts with 0, and /j/0… is not a guest route.
    if (value.startsWith('0')) {
      setLocalError('no_session');
      return;
    }
    // GuestPage already rejected this code: leave through /join so that /j/{code} loads it again.
    if (value === code)
      navigate('/join', { replace: true, state: { retry: value } satisfies RetryState });
    else navigate(`/j/${value}`, { replace: true });
  };

  // Arrived to retry the same code: continue to it now.
  useEffect(() => {
    if (retry && retry.length === JOIN_CODE_LENGTH) navigate(`/j/${retry}`, { replace: true });
  }, [retry, navigate]);

  // Focus: the first box on a fresh keypad, the error after a wrong code.
  useEffect(() => {
    if (shown) errorRef.current?.focus();
    else if (!digits.join('')) boxes.current[0]?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shown]);

  const fill = (start: number, text: string) => {
    const incoming = onlyDigits(text);
    if (!incoming) return;
    const next = [...digits];
    // A whole code (paste, one-time-code autofill) replaces everything; a digit or two lands from the box it was typed in.
    const from = incoming.length >= JOIN_CODE_LENGTH ? 0 : start;
    for (let i = 0; i < incoming.length && from + i < JOIN_CODE_LENGTH; i++)
      next[from + i] = incoming[i];
    setDigits(next);
    setTyped(true);
    setLocalError(null);
    const last = Math.min(from + incoming.length, JOIN_CODE_LENGTH - 1);
    boxes.current[last]?.focus();
    if (next.every(Boolean)) go(next);
  };

  const onKeyDown = (position: number) => (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Backspace') {
      event.preventDefault();
      const target = digits[position] ? position : Math.max(position - 1, 0);
      setDigits(digits.map((d, i) => (i === target ? '' : d)));
      setTyped(true);
      setLocalError(null);
      boxes.current[target]?.focus();
    } else if (event.key === 'ArrowLeft') boxes.current[Math.max(position - 1, 0)]?.focus();
    else if (event.key === 'ArrowRight')
      boxes.current[Math.min(position + 1, JOIN_CODE_LENGTH - 1)]?.focus();
  };

  const onPaste = (position: number) => (event: ClipboardEvent<HTMLInputElement>) => {
    event.preventDefault();
    fill(position, event.clipboardData.getData('text'));
  };

  const attempted = digits.join('');

  return (
    <main
      data-testid={JOIN_CODE_TEST_IDS.page}
      className="mx-auto flex min-h-dvh w-full max-w-md flex-col gap-4 px-4 py-6"
    >
      <h1 className="text-2xl font-bold">Join a presentation</h1>
      <p>Type the code on the screen. It joins as soon as you type the last digit.</p>
      <div role="group" aria-label="Join code" className="flex items-center gap-1.5">
        {positions.map((position) => (
          <span key={position} className="contents">
            {position === 3 && <span aria-hidden="true" className="w-2 shrink-0 border-t-2" />}
            <input
              ref={(element) => {
                boxes.current[position] = element;
              }}
              data-testid={joinCodeBoxTestId(position + 1)}
              aria-label={`Digit ${position + 1} of ${JOIN_CODE_LENGTH}`}
              aria-invalid={shown ? true : undefined}
              aria-describedby={shown ? JOIN_CODE_TEST_IDS.error : undefined}
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              autoComplete={position === 0 ? 'one-time-code' : 'off'}
              value={digits[position]}
              onChange={(event) => fill(position, event.target.value)}
              onKeyDown={onKeyDown(position)}
              onPaste={onPaste(position)}
              onFocus={(event) => event.target.select()}
              className="h-14 min-w-0 flex-1 rounded-lg border-2 text-center text-2xl font-semibold"
            />
          </span>
        ))}
      </div>
      {shown && (
        <p
          ref={errorRef}
          id={JOIN_CODE_TEST_IDS.error}
          data-testid={JOIN_CODE_TEST_IDS.error}
          role="alert"
          tabIndex={-1}
          className="rounded-lg border p-3"
        >
          {shown === 'no_session' && attempted.length === JOIN_CODE_LENGTH
            ? `No live presentation uses ${attempted}. ${ADVICE.no_session}`
            : ADVICE[shown]}
        </p>
      )}
      {shown && (
        <button
          type="button"
          className="min-h-12 rounded-lg border px-4"
          onClick={() => {
            setDigits(positions.map(() => ''));
            setTyped(true);
            setLocalError(null);
            boxes.current[0]?.focus();
          }}
        >
          Clear and try again
        </button>
      )}
    </main>
  );
}

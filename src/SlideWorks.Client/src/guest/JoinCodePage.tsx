import {
  Fragment,
  useEffect,
  useRef,
  useState,
  type ClipboardEvent,
  type FocusEvent,
  type KeyboardEvent,
} from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { JOIN_CODE_LENGTH, JOIN_CODE_TEST_IDS, joinCodeBoxTestId } from './testIds';

// Why the digits are being asked for again: the code has no session, or too many wrong codes were tried. The page says
// what to check and keeps the digits so the guest can fix one. It never shows a 404.
export type JoinCodeError = 'no_session' | 'too_many_attempts';

// What the keypad itself can say on top of what GuestPage reports.
type Shown = JoinCodeError | 'leading_zero';

const ADVICE: Record<Shown, string> = {
  no_session: 'Check the code on the screen. Did you swap two digits?',
  too_many_attempts: 'Too many tries. Wait a minute and try again.',
  leading_zero: "Join codes don't start with 0. Check the code on the screen.",
};

const positions = Array.from({ length: JOIN_CODE_LENGTH }, (_, index) => index);
// A whole code can arrive appended to a digit already in the box (one-time-code autofill): the code is the last six.
const onlyDigits = (text: string) => text.replace(/\D/g, '').slice(-JOIN_CODE_LENGTH);
const spaced = (code: string) => `${code.slice(0, 3)} ${code.slice(3)}`;

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
  // The digits as the next event will see them: focus handlers run before React renders a change.
  const current = useRef(digits);
  const [typed, setTyped] = useState(false);
  const [localError, setLocalError] = useState<Shown | null>(null);
  const [errorTick, setErrorTick] = useState(0);
  const boxes = useRef<(HTMLInputElement | null)[]>([]);
  const errorRef = useRef<HTMLDivElement>(null);
  const shown: Shown | null = localError ?? (typed ? null : (error ?? null));

  const update = (next: string[]) => {
    current.current = next;
    setDigits(next);
    setTyped(true);
    setLocalError(null);
  };

  const go = (next: string[]) => {
    const value = next.join('');
    // No join code starts with 0, and /j/0… is not a guest route.
    if (value.startsWith('0')) {
      setLocalError('leading_zero');
      setErrorTick((tick) => tick + 1);
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

  // Focus: the first box on a fresh keypad, the error after a wrong code (every time one is shown).
  useEffect(() => {
    if (shown) errorRef.current?.focus();
    else if (!digits.join('')) boxes.current[0]?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shown, errorTick]);

  const fill = (start: number, text: string) => {
    const incoming = onlyDigits(text);
    const next = [...current.current];
    // Emptied by the Delete key, Cut or a keyboard that sends no Backspace: clear the box.
    if (!incoming) {
      next[start] = '';
      update(next);
      return;
    }
    // A whole code (paste, one-time-code autofill) replaces everything; a digit or two lands from the box it was typed in.
    const from = incoming.length >= JOIN_CODE_LENGTH ? 0 : start;
    for (let i = 0; i < incoming.length && from + i < JOIN_CODE_LENGTH; i++)
      next[from + i] = incoming[i];
    update(next);
    const last = Math.min(from + incoming.length, JOIN_CODE_LENGTH - 1);
    boxes.current[last]?.focus();
    if (next.every(Boolean)) go(next);
  };

  // Backspace in an empty box goes back one box; a box with a digit is emptied by its input event (see fill).
  const onKeyDown = (position: number) => (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Backspace' && !current.current[position]) {
      event.preventDefault();
      boxes.current[Math.max(position - 1, 0)]?.focus();
    } else if (event.key === 'ArrowLeft') boxes.current[Math.max(position - 1, 0)]?.focus();
    else if (event.key === 'ArrowRight')
      boxes.current[Math.min(position + 1, JOIN_CODE_LENGTH - 1)]?.focus();
  };

  const onFocus = (position: number) => (event: FocusEvent<HTMLInputElement>) => {
    // An empty box after a gap sends focus to the first empty box, so the digits go in order.
    if (!current.current[position]) {
      const first = current.current.findIndex((d) => !d);
      if (first >= 0 && first < position) {
        boxes.current[first]?.focus();
        return;
      }
    }
    event.target.select();
  };

  const onPaste = (position: number) => (event: ClipboardEvent<HTMLInputElement>) => {
    event.preventDefault();
    fill(position, event.clipboardData.getData('text'));
  };

  const attempted = digits.join('');

  return (
    <main
      data-testid={JOIN_CODE_TEST_IDS.page}
      className="mx-auto flex min-h-dvh w-full max-w-md flex-col gap-3.5 px-5 py-5 text-(--sw-night-ink) [background:var(--sw-night-surface)]"
    >
      <header className="flex items-center gap-2">
        <span
          aria-hidden="true"
          className="grid size-[22px] flex-none place-items-center rounded-md bg-(--sw-accent) font-head text-[11px] font-extrabold text-white"
        >
          S
        </span>
        <b className="font-head text-sm">SlideWorks</b>
      </header>
      <h1 className="mt-3.5 font-head text-2xl leading-tight font-extrabold tracking-tight">
        Join a presentation
      </h1>
      <p className="text-[13.5px] leading-normal text-(--sw-night-2)">
        Type the code on the screen.
      </p>
      <div
        role="group"
        aria-label="Join code"
        className="grid grid-cols-[repeat(3,1fr)_10px_repeat(3,1fr)] gap-1.5"
      >
        {positions.map((position) => (
          <Fragment key={position}>
            {position === 3 && <span aria-hidden="true" />}
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
              onFocus={onFocus(position)}
              className="h-14 min-w-0 rounded-[10px] border-[1.5px] border-white/15 bg-white/8 text-center font-head text-[22px] font-bold text-(--sw-night-ink) focus:border-(--sw-accent-glow) focus:ring-[3px] focus:ring-(--sw-accent-glow)/25 focus:outline-none aria-invalid:border-red-400"
            />
          </Fragment>
        ))}
      </div>
      {!shown && (
        <p className="text-[11.5px] leading-normal text-(--sw-night-muted)">
          It joins as soon as you type the last digit.
        </p>
      )}
      {shown && (
        <div
          ref={errorRef}
          id={JOIN_CODE_TEST_IDS.error}
          data-testid={JOIN_CODE_TEST_IDS.error}
          role="alert"
          tabIndex={-1}
          className="flex gap-2 rounded-[10px] bg-red-600/16 p-3 text-[12.5px] leading-snug text-red-200 focus-visible:outline-2 focus-visible:outline-red-400"
        >
          <svg
            aria-hidden="true"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.2"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="mt-px flex-none"
          >
            <path d="M12 3 2 21h20L12 3Z" />
            <path d="M12 10v5M12 18h.01" />
          </svg>
          <span>
            {shown === 'no_session' && attempted.length === JOIN_CODE_LENGTH ? (
              <>
                No live presentation uses <b>{spaced(attempted)}</b>. {ADVICE.no_session}
              </>
            ) : (
              ADVICE[shown]
            )}
          </span>
        </div>
      )}
      {shown && (
        <button
          type="button"
          className="mt-auto flex min-h-12 w-full items-center justify-center rounded-[14px] bg-white/10 text-base font-bold text-(--sw-night-ink) focus-visible:outline-2 focus-visible:outline-(--sw-accent-glow)"
          onClick={() => {
            update(positions.map(() => ''));
            boxes.current[0]?.focus();
          }}
        >
          Clear and try again
        </button>
      )}
    </main>
  );
}

import { useRetry, type Retry } from './useRetry';

export const THROTTLED_MESSAGE = 'Too many tries right now. Trying again in a few seconds.';

// The "Try again" button of the cookies and error screens. While its call is in flight it shows "Trying…" and ignores
// further presses; when the venue is rate limited it says so and tries again by itself, so a press never looks like it
// did nothing.
export function GuestRetryButton({
  onRetry,
  testId,
  random,
}: {
  onRetry: Retry;
  testId: string;
  random?: () => number;
}) {
  const { pending, throttled, run } = useRetry(onRetry, random);
  return (
    <>
      {throttled && (
        <p role="status" className="text-base text-(--sw-night-2)">
          {THROTTLED_MESSAGE}
        </p>
      )}
      <button
        type="button"
        data-testid={testId}
        aria-label={pending ? 'Trying to join the talk' : 'Try again to join the talk'}
        aria-disabled={pending}
        aria-busy={pending}
        onClick={run}
        className="mt-3 min-h-12 w-full rounded-xl bg-white/10 px-4 text-base font-bold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow) aria-disabled:opacity-60"
      >
        {pending ? 'Trying…' : 'Try again'}
      </button>
    </>
  );
}

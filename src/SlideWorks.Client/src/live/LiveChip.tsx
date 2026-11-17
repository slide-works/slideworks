import { useEffect, useRef, useState } from 'react';
import { useLiveSessionContext } from './LiveSessionProvider';

const BUTTON =
  'inline-flex h-12 min-w-12 shrink-0 cursor-pointer items-center justify-center rounded-md px-3.5 text-xs font-semibold focus-visible:outline-2 focus-visible:outline-[#93a8ff]';
const NEUTRAL = `${BUTTON} bg-white/10 text-white hover:bg-white/15`;

// The live chip: the owner's control for going live and ending (SW-5 P1/P2, mockup section 03). Not live: an accent Go
// live. Live: one green pill with the code and the joined count, the join bar toggle, and a red-tinted End. Drawn for
// the owner only. joinBar is the deck's look.joinBar when the page knows it, so the toggle follows a change made
// elsewhere; with none, the live session's own answer.
export function LiveChip({ deckTitle, joinBar }: { deckTitle: string; joinBar?: boolean }) {
  const session = useLiveSessionContext();
  const { live, liveView, error } = session;
  const announcement = useAnnouncement(live, liveView !== null);

  if (liveView === null) {
    // No live view: not the owner (nothing to show), or the first read failed (show it, with a way to retry).
    if (error === null) return null;
    return (
      <span data-testid="live-chip" className="inline-flex shrink-0 items-center gap-2">
        <span data-testid="live-chip-error" role="alert" className="text-xs text-bad-soft">
          {error}
        </span>
        <button
          type="button"
          data-testid="live-chip-retry"
          aria-label={`Retry reading the live session for ${deckTitle}`}
          className={NEUTRAL}
          onClick={() => void session.retry()}
        >
          Retry
        </button>
      </span>
    );
  }
  const pressed = joinBar ?? liveView.joinBar;

  return (
    <span data-testid="live-chip" className="inline-flex shrink-0 items-center gap-2">
      <span role="status" className="sr-only" data-testid="live-chip-announcement">
        {announcement}
      </span>
      {live ? (
        <>
          <span
            role="group"
            aria-label={`Live, code ${liveView.codeLabel}`}
            className="inline-flex items-center gap-2 rounded-md bg-ok/25 px-2.5 py-2 text-xs font-semibold text-ok-soft"
          >
            <i aria-hidden="true" className="size-[7px] rounded-full bg-ok-soft" />
            <span aria-hidden="true">Live</span>
            <b data-testid="live-chip-code" className="font-mono font-medium text-white">
              {liveView.codeLabel}
            </b>
            <span aria-hidden="true">·</span>
            <span data-testid="live-chip-count">{session.joinedCount} joined</span>
          </span>
          <button
            type="button"
            data-testid="live-chip-join-bar"
            aria-label={`Join bar for ${deckTitle}`}
            aria-pressed={pressed}
            className={`${NEUTRAL} ${pressed ? 'outline-2 -outline-offset-2 outline-white/60' : ''}`}
            onClick={() => void session.setJoinBar(!pressed)}
          >
            Join bar
          </button>
          <button
            type="button"
            data-testid="live-chip-end"
            aria-label={`End live session for ${deckTitle}`}
            className={`${BUTTON} bg-bad/30 text-bad-soft hover:bg-bad/40`}
            onClick={() => void session.end()}
          >
            End
          </button>
        </>
      ) : (
        <button
          type="button"
          data-testid="live-chip-go-live"
          aria-label={`Go live for ${deckTitle}`}
          className={`${BUTTON} bg-accent text-white hover:bg-accent-dark`}
          onClick={() => void session.goLive()}
        >
          Go live
        </button>
      )}
      {error && (
        <span data-testid="live-chip-error" role="alert" className="text-xs text-bad-soft">
          {error}
        </span>
      )}
    </span>
  );
}

// The words a screen reader hears, only when the session goes live or ends. The joined count is never announced.
function useAnnouncement(live: boolean, known: boolean): string {
  const [message, setMessage] = useState('');
  const previous = useRef<boolean | null>(null);
  useEffect(() => {
    if (!known) return;
    if (previous.current !== null && previous.current !== live) {
      setMessage(live ? 'You are live' : 'Live session ended');
    }
    previous.current = live;
  }, [live, known]);
  return message;
}

import { useState } from 'react';
import { useLiveSessionContext } from './LiveSessionProvider';

const BUTTON =
  'inline-flex h-12 min-w-12 cursor-pointer items-center justify-center rounded-lg bg-white/10 px-3 text-slate-200 hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-[#93a8ff]';

// The live chip: the owner's control for going live and ending (SW-5 P1/P2). Drawn for the owner only.
// TODO: replace this stub with the real chip: confirmation to end and the real failure messages.
export function LiveChip({ deckTitle }: { deckTitle: string }) {
  const session = useLiveSessionContext();
  const [error, setError] = useState<string | null>(null);
  if (session.liveView === null) return null;
  const { live, joinedCount, liveView } = session;

  const run = async (action: () => Promise<void>, failure: string) => {
    setError(null);
    try {
      await action();
    } catch {
      setError(failure);
    }
  };

  return (
    <span data-testid="live-chip" className="inline-flex shrink-0 items-center gap-2">
      {live ? (
        <>
          <span data-testid="live-chip-code">{liveView.codeLabel}</span>
          <span data-testid="live-chip-count" role="status">
            {joinedCount} joined
          </span>
          <button
            type="button"
            data-testid="live-chip-join-bar"
            aria-label={`Join bar for ${deckTitle}`}
            aria-pressed={liveView.joinBar}
            className={BUTTON}
            onClick={() =>
              void run(
                () => session.setJoinBar(!liveView.joinBar),
                'Could not change the join bar.',
              )
            }
          >
            Join bar
          </button>
          <button
            type="button"
            data-testid="live-chip-end"
            aria-label={`End live session for ${deckTitle}`}
            className={BUTTON}
            onClick={() => void run(session.end, 'Could not end the live session.')}
          >
            End
          </button>
        </>
      ) : (
        <button
          type="button"
          data-testid="live-chip-go-live"
          aria-label={`Go live for ${deckTitle}`}
          className={BUTTON}
          onClick={() => void run(session.goLive, 'Could not go live.')}
        >
          Go live
        </button>
      )}
      <span data-testid="live-chip-error" role="alert">
        {error}
      </span>
    </span>
  );
}

import { useLiveSessionContext } from './LiveSessionProvider';

// The live chip: the owner's control for going live and ending (SW-5 P1/P2). Drawn for the owner only.
// TODO: replace this stub with the real chip: the joined count, the join bar toggle and a confirmation to end.
export function LiveChip({ deckTitle }: { deckTitle: string }) {
  const session = useLiveSessionContext();
  if (session.liveView === null) return null;
  const { live, joinedCount } = session;
  return (
    <span data-testid="live-chip" className="inline-flex shrink-0 items-center gap-2">
      <button
        type="button"
        data-testid="live-chip-toggle"
        aria-label={`${live ? 'End live session' : 'Go live'} for ${deckTitle}`}
        aria-pressed={live}
        className="inline-flex h-12 min-w-12 cursor-pointer items-center rounded-lg bg-white/10 px-3 text-slate-200 hover:bg-white/15 focus-visible:outline-2 focus-visible:outline-[#93a8ff]"
        onClick={() => void (live ? session.end() : session.goLive())}
      >
        {live ? 'Live' : 'Go live'}
      </button>
      {live && (
        <span data-testid="live-chip-count" aria-label={`${joinedCount} joined`}>
          {joinedCount}
        </span>
      )}
    </span>
  );
}

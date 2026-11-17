import { formatClock, formatElapsed } from '../presentation/format';
import type { PresentationRole } from '../presentation/types';
import { usePresentation } from '../presentation/usePresentation';
import { useDeckRealtime } from '../realtime/useDeckRealtime';

type Props = { deckId: string; role?: PresentationRole; label?: string; followDeck?: boolean };

// Component tests only: shows what usePresentation returns, with buttons for its calls. With followDeck it also follows
// the deck's events through useDeckRealtime, the way a page doing both does.
export function PresentationHarness({
  deckId,
  role = 'presenter',
  label = 'Chrome on Linux',
  followDeck = false,
}: Props) {
  const presentation = usePresentation(deckId, role, label);
  const { state } = presentation;
  return (
    <>
      {followDeck && <DeckFollower deckId={deckId} />}
      <output data-testid="status">{presentation.status}</output>
      <output data-testid="position">{state ? `${state.slide}.${state.step}` : ''}</output>
      <output data-testid="version">{state ? `${state.epoch}/${state.seq}` : ''}</output>
      <output data-testid="timer">{state?.timer.state ?? ''}</output>
      <output data-testid="elapsed">{formatElapsed(presentation.elapsedMs)}</output>
      <output data-testid="clock">
        {presentation.clockMinutes === null ? '' : formatClock(presentation.clockMinutes)}
      </output>
      <button
        type="button"
        data-testid="next"
        onClick={() =>
          state &&
          void presentation
            .update({
              slide: state.slide + 1,
              step: 0,
              black: state.black,
              skipDeepDives: state.skipDeepDives,
            })
            .catch(() => undefined)
        }
      >
        Next slide
      </button>
      <button
        type="button"
        data-testid="pause"
        onClick={() => void presentation.timer('pause').catch(() => undefined)}
      >
        Pause the timer
      </button>
    </>
  );
}

function DeckFollower({ deckId }: { deckId: string }) {
  useDeckRealtime(deckId, () => undefined);
  return null;
}

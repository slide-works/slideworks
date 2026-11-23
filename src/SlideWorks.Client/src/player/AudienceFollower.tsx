import { useCallback, useMemo, useState } from 'react';
import type { DeckPlayView } from '../api/types';
import type { PresentationUpdate } from '../presentation/types';
import { deviceLabel } from '../presentation/deviceLabel';
import { usePresentation } from '../presentation/usePresentation';
import { Player, type AudienceControl } from './Player';

// The player in audience mode (P8): joins the deck's presentation as an audience window and follows its state. A
// refused join (not the deck's owner, or the deck is gone) means the plain player, which is the one allowed
// fallback: the play view doesn't say who owns the deck, so the refusal is how the page learns it.
export function AudienceFollower({ deck }: { deck: DeckPlayView }) {
  // Computed once: a new label would rejoin the presentation.
  const [label] = useState(() => deviceLabel(navigator.userAgent, 'Browser'));
  const presentation = usePresentation(deck.id, 'audience', label);
  const { state, status, update } = presentation;
  const [problem, setProblem] = useState<string | null>(null);

  const send = useCallback(
    (to: PresentationUpdate) => {
      update(to).then(
        () => setProblem(null),
        () => setProblem("Couldn't move the presentation. Try again."),
      );
    },
    [update],
  );

  const refused = status === 'refused';
  const slide = state?.slide;
  const step = state?.step;
  const black = state?.black ?? false;
  const skipDeepDives = state?.skipDeepDives ?? false;
  const driven = state?.connected.some((c) => c.role !== 'audience') ?? false;
  const live = status === 'live';
  // Stable between the clock's ticks, so the player's key handlers aren't rebuilt every second.
  const audience = useMemo<AudienceControl | undefined>(
    () =>
      refused
        ? undefined
        : {
            position: slide === undefined || step === undefined ? null : { slide, step },
            live,
            driven,
            black,
            skipDeepDives,
            send,
          },
    [refused, slide, step, live, driven, black, skipDeepDives, send],
  );

  const notice = problem && (
    <p
      role="alert"
      data-testid="audience-error"
      className="fixed top-3 left-1/2 -translate-x-1/2 rounded-lg bg-black/80 px-4 py-2 font-sans text-sm text-white"
    >
      {problem}
    </p>
  );

  return (
    <div data-testid={refused ? undefined : 'audience-mode'} className="h-dvh">
      <Player deck={deck} audience={audience} notice={notice} />
    </div>
  );
}

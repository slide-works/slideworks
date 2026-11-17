import { useCallback, useState } from 'react';
import type { DeckPlayView } from '../api/types';
import type { PresentationUpdate } from '../presentation/types';
import { usePresentation } from '../presentation/usePresentation';
import { Player, type AudienceControl } from './Player';

// Names this window in the presentation's connection list. Constant, so the shared connection is never rejoined
// because the label changed.
const LABEL = 'Audience window';

// The player in audience mode (P8): joins the deck's presentation as an audience window and follows its state. A
// refused join (not the deck's owner, or the deck is gone) means the plain player, which is the one allowed
// fallback: the play view doesn't say who owns the deck, so the refusal is how the page learns it.
export function AudienceFollower({ deck }: { deck: DeckPlayView }) {
  const presentation = usePresentation(deck.id, 'audience', LABEL);
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
  const audience: AudienceControl | undefined = refused
    ? undefined
    : {
        position: state ? { slide: state.slide, step: state.step } : null,
        live: status === 'live',
        driven: state?.connected.some((c) => c.role !== 'audience') ?? false,
        black: state?.black ?? false,
        skipDeepDives: state?.skipDeepDives ?? false,
        send,
      };

  return (
    <div data-testid={refused ? undefined : 'audience-mode'} className="h-dvh">
      <Player deck={deck} audience={audience} />
      {problem && (
        <p
          role="alert"
          data-testid="audience-error"
          className="fixed top-3 left-1/2 -translate-x-1/2 rounded-lg bg-black/80 px-4 py-2 font-sans text-sm text-white"
        >
          {problem}
        </p>
      )}
    </div>
  );
}

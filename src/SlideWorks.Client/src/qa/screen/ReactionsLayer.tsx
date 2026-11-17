import { QA_REACTIONS_LAYER_TEST_IDS } from '../testIds';
import { useScreenReactions } from './useScreenReactions';

// The floating reactions up the right edge of the big screen (SW-9 Q9), mounted once over the player's canvas. With no
// provider (useScreenReactions() null) it renders nothing, so the deck page, RenderPage and thumbnails are unchanged.
// TODO: draws only an empty layer; the real layer floats each burst up the right edge for about 2.5 seconds, at most 30
// at once, then a "+N" chip.
export function ReactionsLayer() {
  const reactions = useScreenReactions();
  if (reactions === null) return null;
  return (
    <div
      data-testid={QA_REACTIONS_LAYER_TEST_IDS.layer}
      className="pointer-events-none absolute inset-0"
    />
  );
}

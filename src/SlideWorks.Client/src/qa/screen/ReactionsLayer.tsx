import { useEffect, useState, useSyncExternalStore } from 'react';
import { reactionOf } from '../emoji';
import { QA_REACTIONS_LAYER_TEST_IDS } from '../testIds';
import type { ReactionEmoji } from '../types';
import styles from './ReactionsLayer.module.css';
import { useScreenReactions, type ScreenBurst } from './useScreenReactions';

// At most this many emoji float at once; the rest of a batch folds into a "+N" chip.
const MAX_FLOATING = 30;
// How long a chip stays after the last tap it counted.
const CHIP_MS = 2500;
// Where along the right edge a float rises, in canvas px from the edge: five lanes, so neighbours don't stack.
const LANES = 5;
const LANE_PX = 36;
const EDGE_PX = 48;

const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';
const subscribeMotion = (notify: () => void) => {
  const query = window.matchMedia(REDUCED_MOTION);
  query.addEventListener('change', notify);
  return () => query.removeEventListener('change', notify);
};
const prefersReducedMotion = () => window.matchMedia(REDUCED_MOTION).matches;

type Chips = Partial<Record<ReactionEmoji, number>>;
type Shown = { seen: number; floats: ScreenBurst[]; chips: Chips };

// The floating reactions up the right edge of the big screen (SW-9 Q9), mounted once over the player's canvas. With no
// provider (useScreenReactions() null) it renders nothing, so the deck page, RenderPage and thumbnails are unchanged.
// Each burst the provider hands over floats up for about 2.5 seconds (a CSS animation; the float is retired when it
// ends). At most 30 float at once: the rest of a batch folds into a "+N {emoji}" chip, and under reduced motion every
// burst does, so nothing moves. A burst is new when its id is above the highest one drawn.
export function ReactionsLayer() {
  const reactions = useScreenReactions();
  const reduced = useSyncExternalStore(subscribeMotion, prefersReducedMotion, () => false);
  const [shown, setShown] = useState<Shown>({ seen: 0, floats: [], chips: {} });

  const fresh = reactions === null ? [] : reactions.bursts.filter((burst) => burst.id > shown.seen);
  if (fresh.length > 0) {
    const room = reduced ? 0 : Math.max(0, MAX_FLOATING - shown.floats.length);
    const chips = { ...shown.chips };
    for (const burst of fresh.slice(room)) chips[burst.emoji] = (chips[burst.emoji] ?? 0) + 1;
    setShown({
      seen: Math.max(...fresh.map((burst) => burst.id)),
      floats: [...shown.floats, ...fresh.slice(0, room)],
      chips,
    });
  }

  const hasChips = Object.keys(shown.chips).length > 0;
  useEffect(() => {
    if (!hasChips) return;
    const timer = setTimeout(() => setShown((held) => ({ ...held, chips: {} })), CHIP_MS);
    return () => clearTimeout(timer);
  }, [hasChips, shown.seen]);

  if (reactions === null) return null;
  const retire = (id: number) =>
    setShown((held) => ({ ...held, floats: held.floats.filter((float) => float.id !== id) }));
  return (
    <div
      data-testid={QA_REACTIONS_LAYER_TEST_IDS.layer}
      aria-hidden="true"
      className="pointer-events-none absolute inset-0"
    >
      {shown.floats.map((float) => (
        <span
          key={float.id}
          data-testid={QA_REACTIONS_LAYER_TEST_IDS.float}
          className={styles.float}
          style={{ right: EDGE_PX + (float.id % LANES) * LANE_PX }}
          onAnimationEnd={() => retire(float.id)}
        >
          {reactionOf(float.emoji).emoji}
        </span>
      ))}
      <div className={styles.chips}>
        {Object.entries(shown.chips).map(([emoji, count]) => (
          <span key={emoji} data-testid={QA_REACTIONS_LAYER_TEST_IDS.chip} className={styles.chip}>
            {`+${count} ${reactionOf(emoji as ReactionEmoji).emoji}`}
          </span>
        ))}
      </div>
    </div>
  );
}

import { useCallback, useEffect, useState, useSyncExternalStore } from 'react';
import { REACTION_EMOJI, reactionOf } from '../emoji';
import { QA_REACTIONS_LAYER_TEST_IDS } from '../testIds';
import type { ReactionEmoji } from '../types';
import styles from './ReactionsLayer.module.css';
import { useScreenReactions, type ScreenBurst } from './useScreenReactions';

// At most this many emoji float at once; the rest of a batch folds into a "+N" chip.
const MAX_FLOATING = 30;
// How long a chip stays after the last tap it counted, each emoji's chip on its own.
const CHIP_MS = 2500;
// A float's animation lasts 2.5 s (ReactionsLayer.module.css). If animationend never comes (a background tab runs no
// animations), a timer retires the float this much later, so it can't hold a slot of the cap forever.
const FLOAT_MS = 2500;
const FLOAT_MARGIN_MS = 500;
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

// taps counts the folds into this chip: each one restarts that chip's own timer and no other.
type Chip = { count: number; taps: number };
type Chips = Partial<Record<ReactionEmoji, Chip>>;
type Shown = { seen: number; floats: ScreenBurst[]; chips: Chips };

// The floating reactions up the right edge of the big screen (SW-9 Q9), mounted once over the player's canvas. With no
// provider (useScreenReactions() null) it renders nothing, so the deck page, RenderPage and thumbnails are unchanged.
// Each burst the provider hands over floats up for about 2.5 seconds (a CSS animation; the float is retired when it
// ends, or by a timer if it never does). At most 30 float at once: the rest of a batch folds into a "+N {emoji}" chip, and under reduced motion every
// burst does, so nothing moves. A burst is new when its id is above the highest one drawn (ids only increase, and an
// empty list from the provider starts the count again). Each emoji's chip clears 2.5 s after its own last tap.
export function ReactionsLayer() {
  const reactions = useScreenReactions();
  const reduced = useSyncExternalStore(subscribeMotion, prefersReducedMotion, () => false);
  const [shown, setShown] = useState<Shown>({ seen: 0, floats: [], chips: {} });

  // A provider whose list is empty has nothing left to draw: a stale highest id must not hide what it sends next.
  if (reactions !== null && reactions.bursts.length === 0 && shown.seen !== 0) {
    setShown({ ...shown, seen: 0 });
  }

  const fresh = reactions === null ? [] : reactions.bursts.filter((burst) => burst.id > shown.seen);
  if (fresh.length > 0) {
    const room = reduced ? 0 : Math.max(0, MAX_FLOATING - shown.floats.length);
    const chips = { ...shown.chips };
    for (const burst of fresh.slice(room)) {
      const held = chips[burst.emoji];
      chips[burst.emoji] = { count: (held?.count ?? 0) + 1, taps: (held?.taps ?? 0) + 1 };
    }
    setShown({
      seen: Math.max(...fresh.map((burst) => burst.id)),
      floats: [...shown.floats, ...fresh.slice(0, room)],
      chips,
    });
  }

  const retire = useCallback(
    (id: number) =>
      setShown((held) => ({ ...held, floats: held.floats.filter((float) => float.id !== id) })),
    [],
  );
  const expire = useCallback((emoji: ReactionEmoji, taps: number) => {
    setShown((held) => {
      // Only the tap that started this timer may clear the chip: a later tap has its own.
      if (held.chips[emoji]?.taps !== taps) return held;
      const chips = { ...held.chips };
      delete chips[emoji];
      return { ...held, chips };
    });
  }, []);

  if (reactions === null) return null;
  return (
    <div
      data-testid={QA_REACTIONS_LAYER_TEST_IDS.layer}
      aria-hidden="true"
      className="pointer-events-none absolute inset-0"
    >
      {shown.floats.map((float) => (
        <FloatingEmoji key={float.id} burst={float} onRetire={retire} />
      ))}
      <div className={styles.chips}>
        {REACTION_EMOJI.map((emoji) => {
          const chip = shown.chips[emoji];
          return chip === undefined ? null : (
            <ChipView key={emoji} emoji={emoji} chip={chip} onExpire={expire} />
          );
        })}
      </div>
    </div>
  );
}

function FloatingEmoji({
  burst,
  onRetire,
}: {
  burst: ScreenBurst;
  onRetire: (id: number) => void;
}) {
  useEffect(() => {
    const timer = setTimeout(() => onRetire(burst.id), FLOAT_MS + FLOAT_MARGIN_MS);
    return () => clearTimeout(timer);
  }, [burst.id, onRetire]);
  return (
    <span
      data-testid={QA_REACTIONS_LAYER_TEST_IDS.float}
      className={styles.float}
      style={{ right: EDGE_PX + (burst.id % LANES) * LANE_PX }}
      onAnimationEnd={() => onRetire(burst.id)}
    >
      {reactionOf(burst.emoji).emoji}
    </span>
  );
}

// One emoji's chip, with its own timer: it restarts only when this emoji folds again.
function ChipView({
  emoji,
  chip,
  onExpire,
}: {
  emoji: ReactionEmoji;
  chip: Chip;
  onExpire: (emoji: ReactionEmoji, taps: number) => void;
}) {
  useEffect(() => {
    const timer = setTimeout(() => onExpire(emoji, chip.taps), CHIP_MS);
    return () => clearTimeout(timer);
  }, [emoji, chip.taps, onExpire]);
  return (
    <span data-testid={QA_REACTIONS_LAYER_TEST_IDS.chip} className={styles.chip}>
      {`+${chip.count} ${reactionOf(emoji).emoji}`}
    </span>
  );
}

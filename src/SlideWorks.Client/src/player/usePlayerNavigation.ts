import { useCallback, useEffect, useMemo, useState } from 'react';
import { FIRST_POSITION, formatHash, parseHash, type PlayerPosition } from './parseHash';

// A presentation's position the player follows (P8's audience mode). While live the player shows it and keeps no
// position of its own (D4); otherwise it keeps what the last one said and moves locally.
export type FollowedPosition = { position: PlayerPosition; live: boolean };

export type PlayerNavigation = {
  position: PlayerPosition;
  next(): void;
  previous(): void;
  first(): void;
  last(): void;
};

function positionFromLocation(stepCounts: readonly number[]): PlayerPosition {
  return parseHash(window.location.hash, stepCounts) ?? FIRST_POSITION;
}

// The same position on a deck that changed under the player: a slide past the
// new end becomes the last slide, and a step past its slide's last one is
// clamped. An unchanged position comes back as the same object.
function clampTo(position: PlayerPosition, stepCounts: readonly number[]): PlayerPosition {
  const slide = Math.min(position.slide, Math.max(stepCounts.length, 1));
  const step = Math.min(position.step, stepCounts[slide - 1] ?? 0);
  return slide === position.slide && step === position.step ? position : { slide, step };
}

// The player's position, kept in the address bar as `#slide.step`. A deep link
// is restored on load (anything invalid falls back to slide 1), every move
// rewrites the hash with replaceState so a deck adds no history entry per
// slide, and a hash changed from outside (the user edits it, or Back/Forward)
// moves the player there. Each move is exactly one step, or one slide once the
// slide's steps are all shown. When the deck changes under the player (a live
// refetch), the position is clamped onto it and the hash replaced.
export function usePlayerNavigation(
  stepCounts: readonly number[],
  follow?: FollowedPosition | null,
): PlayerNavigation {
  const [local, setPosition] = useState(() => positionFromLocation(stepCounts));
  const [followed, setFollowed] = useState<PlayerPosition | null>(null);
  const incoming = follow?.position ?? null;
  const following = follow?.live ?? false;
  const differs = (a: PlayerPosition | null, b: PlayerPosition) =>
    a?.slide !== b.slide || a.step !== b.step;
  if (incoming && (differs(followed, incoming) || (following && differs(local, incoming)))) {
    // The next followed position overrides a local one, even an unchanged one after a reconnect: no second position
    // while live (D4). A player that loses its connection stays where it was.
    setFollowed(incoming);
    setPosition(incoming);
  }
  const position = following && incoming ? incoming : local;
  const [clampedFor, setClampedFor] = useState(stepCounts);
  if (clampedFor !== stepCounts) {
    // Adjusting state while rendering, so the stale position never paints.
    setClampedFor(stepCounts);
    setPosition((current) => clampTo(current, stepCounts));
  }

  useEffect(() => {
    const hash = formatHash(position);
    if (window.location.hash !== hash) {
      // Keep the router's own history state; only the hash changes.
      window.history.replaceState(window.history.state, '', hash);
    }
  }, [position]);

  useEffect(() => {
    // No local navigation while following a live presentation (D4).
    if (following) return;
    const goToHash = () => setPosition(positionFromLocation(stepCounts));
    window.addEventListener('hashchange', goToHash);
    window.addEventListener('popstate', goToHash);
    return () => {
      window.removeEventListener('hashchange', goToHash);
      window.removeEventListener('popstate', goToHash);
    };
  }, [stepCounts, following]);

  const total = stepCounts.length;

  const next = useCallback(() => {
    setPosition(({ slide, step }) => {
      if (step < stepCounts[slide - 1]) return { slide, step: step + 1 };
      if (slide < total) return { slide: slide + 1, step: 0 };
      return { slide, step };
    });
  }, [stepCounts, total]);

  const previous = useCallback(() => {
    setPosition(({ slide, step }) => {
      if (step > 0) return { slide, step: step - 1 };
      // Back onto a slide shows it as it was left: every step revealed.
      if (slide > 1) return { slide: slide - 1, step: stepCounts[slide - 2] };
      return { slide, step };
    });
  }, [stepCounts]);

  const first = useCallback(() => setPosition(FIRST_POSITION), []);
  const last = useCallback(() => setPosition({ slide: Math.max(total, 1), step: 0 }), [total]);

  return useMemo(
    () => ({ position, next, previous, first, last }),
    [position, next, previous, first, last],
  );
}

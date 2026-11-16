import { useCallback, useEffect, useMemo, useState } from 'react';
import { FIRST_POSITION, formatHash, parseHash, type PlayerPosition } from './parseHash';

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

// The player's position, kept in the address bar as `#slide.step`. A deep link
// is restored on load (anything invalid falls back to slide 1), every move
// rewrites the hash with replaceState so a deck adds no history entry per
// slide, and a hash changed from outside (the user edits it, or Back/Forward)
// moves the player there. Each move is exactly one step, or one slide once the
// slide's steps are all shown.
export function usePlayerNavigation(stepCounts: readonly number[]): PlayerNavigation {
  const [position, setPosition] = useState(() => positionFromLocation(stepCounts));

  useEffect(() => {
    const hash = formatHash(position);
    if (window.location.hash !== hash) {
      // Keep the router's own history state; only the hash changes.
      window.history.replaceState(window.history.state, '', hash);
    }
  }, [position]);

  useEffect(() => {
    const follow = () => setPosition(positionFromLocation(stepCounts));
    window.addEventListener('hashchange', follow);
    window.addEventListener('popstate', follow);
    return () => {
      window.removeEventListener('hashchange', follow);
      window.removeEventListener('popstate', follow);
    };
  }, [stepCounts]);

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

import { useSyncExternalStore } from 'react';

// The phone turned sideways: the same query the following screen's CSS uses to hide its header and fill the screen.
const LANDSCAPE = '(orientation: landscape) and (max-height: 500px)';

const subscribe = (notify: () => void) => {
  const query = window.matchMedia(LANDSCAPE);
  query.addEventListener('change', notify);
  return () => query.removeEventListener('change', notify);
};

export const useLandscape = () =>
  useSyncExternalStore(
    subscribe,
    () => window.matchMedia(LANDSCAPE).matches,
    () => false,
  );

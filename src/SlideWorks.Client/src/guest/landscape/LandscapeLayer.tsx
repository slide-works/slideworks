import type { ReactNode } from 'react';

// What a phone turned sideways shows over the full-screen slide (SW-883 D6): the slide number, the Live chip, the
// language chip and the progress, for 3 seconds after a tap, and Full screen on Android. index and count are the slide
// on screen, connected is false while the connection comes back, progress is index / count (0 to 1), and languageChip is
// the language chip, or null when the deck has no translations.
// TODO: renders nothing yet; the tap overlay and the Full screen button replace this.
export function LandscapeLayer(props: {
  index: number;
  count: number;
  connected: boolean;
  progress: number;
  languageChip: ReactNode | null;
}): ReactNode {
  void props;
  return null;
}

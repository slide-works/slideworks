import type { DeckPlayView } from '../../api/types';
import { LAYOUTS } from '../../slides/layouts';
import { Player } from '../Player';
import { RevealTestLayout } from './RevealTestLayout';

// Test only: the Player with the bullets layout swapped for RevealTestLayout.
// The swap is made on the registry object at runtime, inside the CT page that
// mounts this component (each test gets a fresh page); the registry's source
// is untouched.
LAYOUTS.bullets = RevealTestLayout;

export function RevealPlayer({ deck }: { deck: DeckPlayView }) {
  return <Player deck={deck} />;
}

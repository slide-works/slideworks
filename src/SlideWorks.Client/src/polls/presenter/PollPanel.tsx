import { POLL_PANEL_TEST_IDS } from '../testIds';
import { usePollControl } from './usePollControl';

// The poll panel in the presenter window (SW-6 V8): the poll's state and round, its settings, the answer count and the
// rate, a bar per option with its count, what the room sees, the five controls, earlier rounds and the keys.
export function PollPanel({ deckId }: { deckId: string }) {
  const { control } = usePollControl(deckId);
  // TODO: the state only. The real panel adds the bars, the controls, the history and the keys.
  return (
    <section data-testid={POLL_PANEL_TEST_IDS.panel} aria-label="Poll">
      {control === null ? (
        <p data-testid={POLL_PANEL_TEST_IDS.empty}>No poll on this slide.</p>
      ) : (
        <p data-testid={POLL_PANEL_TEST_IDS.state}>{control.phase}</p>
      )}
    </section>
  );
}

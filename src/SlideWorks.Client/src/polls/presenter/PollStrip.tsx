import { POLL_STRIP_TEST_IDS } from '../testIds';
import { usePollControl } from './usePollControl';

// The poll strip on the presenter's phone remote (SW-6 V8), between the slide and Next: the state, the answer count
// and the rate, and the controls the phase allows. Nothing at all on a slide with no poll.
export function PollStrip({ deckId }: { deckId: string }) {
  const { control } = usePollControl(deckId);
  // TODO: the state only. The real strip adds the count, the rate and the controls.
  if (control === null) return null;
  return (
    <section data-testid={POLL_STRIP_TEST_IDS.strip} aria-label="Poll">
      <p data-testid={POLL_STRIP_TEST_IDS.state}>{control.phase}</p>
    </section>
  );
}

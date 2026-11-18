import { useQaScreen, useQaScreenState } from '../qa/screen/QaScreenContext';
import { useScreenReactions } from '../qa/screen/useScreenReactions';

// Component tests only: prints what the big screen's two hooks give, as JSON ("null" for no live data), and the Q&A screen's state (none, pending, off or on), so a spec can
// assert a provider's values while the screen parts are still stubs.
export function QaScreenProbe() {
  const screen = useQaScreen();
  const state = useQaScreenState();
  const reactions = useScreenReactions();
  return (
    <div>
      <pre data-testid="probe-qa-screen">{JSON.stringify(screen)}</pre>
      <i data-testid="probe-qa-state">{state.kind}</i>
      <pre data-testid="probe-screen-reactions">{JSON.stringify(reactions)}</pre>
    </div>
  );
}

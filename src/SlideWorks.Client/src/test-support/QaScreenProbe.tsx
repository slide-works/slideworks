import { useQaScreen } from '../qa/screen/QaScreenContext';
import { useScreenReactions } from '../qa/screen/useScreenReactions';

// Component tests only: prints what the big screen's two hooks give, as JSON ("null" for no live data), so a spec can
// assert a provider's values while the screen parts are still stubs.
export function QaScreenProbe() {
  const screen = useQaScreen();
  const reactions = useScreenReactions();
  return (
    <div>
      <pre data-testid="probe-qa-screen">{JSON.stringify(screen)}</pre>
      <pre data-testid="probe-screen-reactions">{JSON.stringify(reactions)}</pre>
    </div>
  );
}

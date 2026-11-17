import { QA_PANEL_TEST_IDS } from '../testIds';
import { QaSettingsStrip } from './QaSettingsStrip';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
};

// The Questions panel in the presenter window (SW-9 Q9), under QaControlLive, opened and closed with Q or the tool
// bar's Questions tool: the tabs Open, To review, Answered and Hidden, the presenter's own sort, each question's
// moderation buttons, the settings strip and the reactions counter.
// TODO: a stub with its final props: an empty root and the settings strip; the real panel draws the view from
// useQaControl with its tabs, sort, rows, buttons, counter and messages.
export function QaPanel({ deckId }: Props) {
  useQaControl(deckId);
  return (
    <section data-testid={QA_PANEL_TEST_IDS.panel} aria-label="Questions" className="contents">
      <QaSettingsStrip deckId={deckId} />
    </section>
  );
}

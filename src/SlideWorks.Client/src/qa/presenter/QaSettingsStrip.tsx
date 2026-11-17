import { QA_SETTINGS_STRIP_TEST_IDS } from '../testIds';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
};

// The settings strip in the Questions panel (SW-9 Q9): the deck's questions, review, names and reactions toggles, and
// the switch of the slide the presenter is on (the view's `here`), each with its reason when the deck has that feature
// off. Changes go through useQaControl's setSettings and setSlide.
// TODO: a stub with its final props: an empty root; the real strip draws the toggles from useQaControl.
export function QaSettingsStrip({ deckId }: Props) {
  useQaControl(deckId);
  return (
    <div
      data-testid={QA_SETTINGS_STRIP_TEST_IDS.strip}
      role="group"
      aria-label="Question settings"
      className="contents"
    />
  );
}

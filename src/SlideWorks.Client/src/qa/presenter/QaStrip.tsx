import { QA_STRIP_TEST_IDS } from '../testIds';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
};

// The questions strip on the presenter's phone remote (SW-9 Q9), under SW-6's poll strip: the top open question with
// its votes and counts, Show or Take off and Answered, and "All 6", which opens a sheet with the open and to-review
// questions and their buttons.
// TODO: a stub with its final props: an empty root; the real strip draws the view from useQaControl.
export function QaStrip({ deckId }: Props) {
  useQaControl(deckId);
  return (
    <section data-testid={QA_STRIP_TEST_IDS.strip} aria-label="Questions" className="contents" />
  );
}

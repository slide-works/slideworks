import { QA_SETTINGS_CARD_TEST_IDS } from '../testIds';
import { useQaControl } from './useQaControl';

type Props = {
  deckId: string;
  // The slide selected on the deck page, 1-based, whose switch the card shows; null when the deck has no slides.
  slide: number | null;
};

// The deck page's "Q&A and reactions" card (SW-9 Q9), for a person who can present the deck, under QaControlLive: the
// deck's five settings and the selected slide's switch, live or not. Changes go through useQaControl's setSettings and
// setSlide.
// TODO: a stub with its final props: an empty root; the real card draws the settings from useQaControl.
export function QaSettingsCard({ deckId }: Props) {
  useQaControl(deckId);
  return (
    <section
      data-testid={QA_SETTINGS_CARD_TEST_IDS.card}
      aria-label="Q&A and reactions"
      className="contents"
    />
  );
}

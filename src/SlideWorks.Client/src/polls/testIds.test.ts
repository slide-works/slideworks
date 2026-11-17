import { describe, expect, it } from 'vitest';
import { GUEST_TEST_IDS } from '../guest/testIds';
import {
  POLL_CARD_TEST_IDS,
  POLL_PANEL_TEST_IDS,
  POLL_SLIDE_TEST_IDS,
  POLL_STRIP_TEST_IDS,
  pollOptionBarTestId,
  pollOptionCorrectTestId,
  pollOptionPercentTestId,
  pollOptionTestId,
  pollOptionYouTestId,
  pollPanelBarTestId,
  pollPanelCorrectTestId,
  pollPanelCountTestId,
  pollPanelHistoryRoundTestId,
  pollPanelRowTestId,
  pollSlideBarTestId,
  pollSlideCorrectTestId,
  pollSlidePercentTestId,
  pollSlideRowTestId,
} from './testIds';

const PER_OPTION = [
  pollOptionTestId,
  pollOptionBarTestId,
  pollOptionPercentTestId,
  pollOptionYouTestId,
  pollOptionCorrectTestId,
  pollSlideRowTestId,
  pollSlideBarTestId,
  pollSlidePercentTestId,
  pollSlideCorrectTestId,
  pollPanelRowTestId,
  pollPanelBarTestId,
  pollPanelCountTestId,
  pollPanelCorrectTestId,
];

describe('poll test ids', () => {
  it('are unique across the card, the slide, the panel, the strip and the guest page', () => {
    const fixed = [
      ...Object.values(POLL_CARD_TEST_IDS),
      ...Object.values(POLL_SLIDE_TEST_IDS),
      ...Object.values(POLL_PANEL_TEST_IDS),
      ...Object.values(POLL_STRIP_TEST_IDS),
      ...Object.values(GUEST_TEST_IDS),
    ];
    // Eight options at most (V1), and a few rounds.
    const numbered = [
      ...PER_OPTION.flatMap((id) => [0, 1, 2, 3, 4, 5, 6, 7].map(id)),
      ...[1, 2, 3].map(pollPanelHistoryRoundTestId),
    ];
    const all = [...fixed, ...numbered];
    expect(new Set(all).size).toBe(all.length);
  });

  it('number options by their 0-based position, like picks', () => {
    expect(pollOptionTestId(0)).toBe('poll-option-0');
    expect(pollSlideBarTestId(7)).toBe('poll-slide-bar-7');
    expect(pollPanelHistoryRoundTestId(1)).toBe('poll-panel-history-round-1');
  });
});

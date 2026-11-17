import { describe, expect, it } from 'vitest';
import { GUEST_TEST_IDS, JOIN_CODE_TEST_IDS, joinCodeBoxTestId } from '../guest/testIds';
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
} from '../polls/testIds';
import { REACTION_EMOJI } from './emoji';
import {
  QA_ASK_SHEET_TEST_IDS,
  QA_CARD_TEST_IDS,
  QA_LIST_TEST_IDS,
  QA_PANEL_TEST_IDS,
  QA_POLL_OPENED_TEST_IDS,
  QA_REACTION_ROW_TEST_IDS,
  QA_REACTIONS_LAYER_TEST_IDS,
  QA_SETTINGS_CARD_TEST_IDS,
  QA_SETTINGS_STRIP_TEST_IDS,
  QA_SLIDE_TEST_IDS,
  QA_STRIP_TEST_IDS,
  QA_TABS_TEST_IDS,
  qaPanelActionTestId,
  qaPanelReactionCountTestId,
  qaPanelRowMetaTestId,
  qaPanelRowTestId,
  qaPanelRowTextTestId,
  qaPanelRowVotesTestId,
  qaSlideRowTestId,
  qaSlideRowTextTestId,
  qaSlideRowVotesTestId,
  qaStripSheetActionTestId,
  qaStripSheetRowTestId,
  questionAnsweredTestId,
  questionHiddenTestId,
  questionMetaTestId,
  questionNoVoteTestId,
  questionOnScreenTestId,
  questionRowTestId,
  questionTextTestId,
  questionUpvoteTestId,
  questionVotesTestId,
  questionWaitingTestId,
  questionYouTestId,
  reactionButtonTestId,
} from './testIds';
import { QA_ACTIONS } from './types';

const QA_FIXED = [
  QA_TABS_TEST_IDS,
  QA_REACTION_ROW_TEST_IDS,
  QA_LIST_TEST_IDS,
  QA_ASK_SHEET_TEST_IDS,
  QA_POLL_OPENED_TEST_IDS,
  QA_SLIDE_TEST_IDS,
  QA_CARD_TEST_IDS,
  QA_REACTIONS_LAYER_TEST_IDS,
  QA_PANEL_TEST_IDS,
  QA_SETTINGS_STRIP_TEST_IDS,
  QA_SETTINGS_CARD_TEST_IDS,
  QA_STRIP_TEST_IDS,
].flatMap((ids) => Object.values(ids));

const PER_QUESTION = [
  questionRowTestId,
  questionTextTestId,
  questionMetaTestId,
  questionUpvoteTestId,
  questionVotesTestId,
  questionNoVoteTestId,
  questionAnsweredTestId,
  questionYouTestId,
  questionWaitingTestId,
  questionOnScreenTestId,
  questionHiddenTestId,
  qaSlideRowTestId,
  qaSlideRowTextTestId,
  qaSlideRowVotesTestId,
  qaPanelRowTestId,
  qaPanelRowTextTestId,
  qaPanelRowMetaTestId,
  qaPanelRowVotesTestId,
  qaStripSheetRowTestId,
  ...QA_ACTIONS.flatMap((action) => [
    (number: number) => qaPanelActionTestId(action, number),
    (number: number) => qaStripSheetActionTestId(action, number),
  ]),
];

const PER_REACTION = [reactionButtonTestId, qaPanelReactionCountTestId];

const PER_POLL_OPTION = [
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

// Question numbers 1 to 12 and 100 to 112, so a two- or three-digit number can't run into another id's suffix.
const NUMBERS = [
  ...Array.from({ length: 12 }, (_, i) => i + 1),
  ...Array.from({ length: 13 }, (_, i) => i + 100),
];

describe('qa test ids', () => {
  it('are unique across qa, the polls and the guest pages', () => {
    const qa = [
      ...QA_FIXED,
      ...PER_QUESTION.flatMap((id) => NUMBERS.map(id)),
      ...PER_REACTION.flatMap((id) => REACTION_EMOJI.map(id)),
    ];
    const others = [
      ...Object.values(POLL_CARD_TEST_IDS),
      ...Object.values(POLL_SLIDE_TEST_IDS),
      ...Object.values(POLL_PANEL_TEST_IDS),
      ...Object.values(POLL_STRIP_TEST_IDS),
      ...PER_POLL_OPTION.flatMap((id) => [0, 1, 2, 3, 4, 5, 6, 7].map(id)),
      ...[1, 2, 3].map(pollPanelHistoryRoundTestId),
      ...Object.values(GUEST_TEST_IDS),
      ...Object.values(JOIN_CODE_TEST_IDS),
      ...[1, 2, 3, 4, 5, 6].map(joinCodeBoxTestId),
    ];
    const all = [...qa, ...others];
    expect(new Set(all).size).toBe(all.length);
  });

  it('all start with qa- and use lower-case kebab case', () => {
    const qa = [
      ...QA_FIXED,
      ...PER_QUESTION.map((id) => id(7)),
      ...PER_REACTION.flatMap((id) => REACTION_EMOJI.map(id)),
    ];
    for (const id of qa) expect(id).toMatch(/^qa-[a-zA-Z0-9]+(-[a-zA-Z0-9]+)*$/);
    for (const id of QA_FIXED) expect(id).toMatch(/^qa(-[a-z0-9]+)+$/);
  });

  it('number questions by their 1-based number, as on the wire', () => {
    expect(questionRowTestId(1)).toBe('qa-question-1');
    expect(questionUpvoteTestId(6)).toBe('qa-question-upvote-6');
    expect(qaSlideRowTestId(2)).toBe('qa-slide-row-2');
    expect(qaPanelActionTestId('hide', 5)).toBe('qa-panel-hide-5');
    expect(qaStripSheetActionTestId('approve', 4)).toBe('qa-strip-sheet-approve-4');
  });

  it('name each reaction by its wire name', () => {
    expect(reactionButtonTestId('thumbsUp')).toBe('qa-reaction-thumbsUp');
    expect(qaPanelReactionCountTestId('clap')).toBe('qa-panel-count-clap');
  });
});

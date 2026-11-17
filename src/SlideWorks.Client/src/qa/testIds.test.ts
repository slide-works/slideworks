import { describe, expect, it } from 'vitest';
import * as guestIds from '../guest/testIds';
import * as pollIds from '../polls/testIds';
import { REACTION_EMOJI } from './emoji';
import * as qaIds from './testIds';
import {
  qaPanelActionTestId,
  qaPanelReactionCountTestId,
  qaSlideRowTestId,
  qaStripSheetActionTestId,
  questionRowTestId,
  questionUpvoteTestId,
  reactionButtonTestId,
} from './testIds';
import { QA_ACTIONS, type QaAction, type ReactionEmoji } from './types';

// Question numbers 1 to 12 and 100 to 112, so a two- or three-digit number can't run into another id's suffix.
const NUMBERS = [
  ...Array.from({ length: 12 }, (_, i) => i + 1),
  ...Array.from({ length: 13 }, (_, i) => i + 100),
];

// The id functions of qa/testIds.ts that take a reaction's wire name. Every other one-argument function takes a
// number (a question's, an option's position, a round's or a join-code box's).
const PER_REACTION: unknown[] = [reactionButtonTestId, qaPanelReactionCountTestId];

type IdFunction =
  | ((value: number) => string)
  | ((emoji: ReactionEmoji) => string)
  | ((action: QaAction, number: number) => string);

// Every id a test-id module exports: each fixed id of each map, and each id function over every argument it can take.
// It walks the module's exports, so an id added later can't be left out. Other exports (JOIN_CODE_LENGTH) are skipped.
function idsOf(module: Record<string, unknown>): string[] {
  return Object.values(module).flatMap((value): string[] => {
    if (typeof value === 'object' && value !== null)
      return Object.values(value).filter((id): id is string => typeof id === 'string');
    if (typeof value !== 'function') return [];
    const id = value as IdFunction;
    if (id.length === 2)
      return QA_ACTIONS.flatMap((action) =>
        NUMBERS.map((n) => (id as (a: QaAction, n: number) => string)(action, n)),
      );
    if (PER_REACTION.includes(value))
      return REACTION_EMOJI.map((emoji) => (id as (e: ReactionEmoji) => string)(emoji));
    return [0, ...NUMBERS].map((n) => (id as (n: number) => string)(n));
  });
}

const fixedOf = (module: Record<string, unknown>) =>
  Object.values(module)
    .filter(
      (value): value is Record<string, unknown> => typeof value === 'object' && value !== null,
    )
    .flatMap((ids) => Object.values(ids));

describe('qa test ids', () => {
  it('are unique across qa, the polls and the guest pages', () => {
    const all = [...idsOf(qaIds), ...idsOf(pollIds), ...idsOf(guestIds)];
    expect(all.length).toBeGreaterThan(1000);
    expect(new Set(all).size).toBe(all.length);
  });

  it('cover every export of the module', () => {
    for (const [name, value] of Object.entries(qaIds)) {
      expect(['object', 'function'], name).toContain(typeof value);
      if (typeof value === 'function') expect([1, 2], name).toContain(value.length);
    }
    expect(fixedOf(qaIds)).toContain('qa-reaction-pop');
  });

  it('all start with qa- and use lower-case kebab case, but for the reaction wire names', () => {
    for (const id of idsOf(qaIds)) expect(id).toMatch(/^qa-[a-zA-Z0-9]+(-[a-zA-Z0-9]+)*$/);
    for (const id of fixedOf(qaIds)) expect(id).toMatch(/^qa(-[a-z0-9]+)+$/);
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

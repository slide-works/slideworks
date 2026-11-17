import { describe, expect, it } from 'vitest';
import { qaControlView } from '../../test-support/fixtures/qaStates';
import type { QaQuestion } from '../types';
import { qaScreenOf } from './QaScreenContext';

const open = (number: number, votes: number, overrides: Partial<QaQuestion> = {}): QaQuestion => ({
  number,
  text: `Question ${number}`,
  name: null,
  age: 'now',
  state: 'open',
  hidden: false,
  onScreen: false,
  votes,
  ...overrides,
});

describe('qaScreenOf', () => {
  it('gives the live sample as the room sees it: open questions only, most votes first, the shown one up', () => {
    expect(qaScreenOf(qaControlView('live'))).toEqual({
      phase: 'open',
      total: 3,
      top: [
        { number: 1, text: 'How do you split a big change?', votes: 14 },
        { number: 2, text: 'What is the smallest slice you have shipped?', votes: 9 },
        { number: 6, text: 'How do you handle database migrations?', votes: 3 },
      ],
      onScreen: {
        number: 2,
        text: 'What is the smallest slice you have shipped?',
        name: null,
        votes: 9,
      },
      showOn: 'anySlide',
    });
  });

  it('is null while not live', () => {
    expect(qaScreenOf(qaControlView('notLive'))).toBeNull();
  });

  it('is null with no round, as with questions off', () => {
    expect(qaScreenOf(qaControlView('live', { round: null }))).toBeNull();
  });

  it('is closed while questions are closed, and carries showOn', () => {
    const control = qaControlView('live');
    control.settings = { ...control.settings, questions: 'closed', showOn: 'qaSlide' };
    expect(qaScreenOf(control)).toMatchObject({ phase: 'closed', showOn: 'qaSlide' });
  });

  it('lists at most five, ties to the older question', () => {
    const questions = [
      open(1, 2),
      open(2, 5),
      open(3, 5),
      open(4, 1),
      open(5, 9),
      open(6, 2),
      open(7, 0),
    ];
    const control = qaControlView('live', { questions, onScreen: null });
    control.counts = { ...control.counts, open: 7 };
    const screen = qaScreenOf(control);
    expect(screen?.top.map((row) => row.number)).toEqual([5, 2, 3, 1, 6]);
    expect(screen?.total).toBe(7);
    expect(screen?.onScreen).toBeNull();
  });

  it('gives no question up when onScreen names a number not in the list', () => {
    const screen = qaScreenOf(qaControlView('live', { onScreen: 99 }));
    expect(screen?.onScreen).toBeNull();
    expect(screen?.top).toHaveLength(3);
  });

  it('leaves hidden, waiting and answered questions off the list', () => {
    const questions = [
      open(1, 9, { hidden: true }),
      open(2, 8, { state: 'waiting' }),
      open(3, 7, { state: 'answered' }),
      open(4, 1),
    ];
    const screen = qaScreenOf(qaControlView('live', { questions, onScreen: null }));
    expect(screen?.top.map((row) => row.number)).toEqual([4]);
  });
});

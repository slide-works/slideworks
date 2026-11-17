import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestPollJson from '@contracts/screens/guest-poll.json' with { type: 'json' };
import pollActivityJson from '@contracts/screens/poll-activity.json' with { type: 'json' };
import pollAnswerJson from '@contracts/screens/poll-answer.json' with { type: 'json' };
import pollControlJson from '@contracts/screens/poll-control.json' with { type: 'json' };
import pollResultsJson from '@contracts/screens/poll-results.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { POLL_REFUSALS } from '../../polls/types';
import {
  answerAccepted,
  answerInvalid,
  answerNoGuest,
  answerNoSession,
  answerRequest,
  answerStale,
  answerTooMany,
  guestPoll,
  pollActivity,
  pollControl,
  pollControlView,
  pollRefusals,
  pollResults,
  pollTally,
} from './pollStates';

const keys = (value: object) => Object.keys(value).sort();
const ACTIVITY_KEYS = [
  'correct',
  'max',
  'number',
  'options',
  'phase',
  'pick',
  'question',
  'results',
  'roomSeesCounts',
  'round',
  'type',
];
const SETTINGS_KEYS = ['correct', 'max', 'opens', 'pick', 'privacy', 'results'];
const CONTROL_KEYS = [
  'answerShown',
  'answered',
  'counts',
  'joined',
  'number',
  'options',
  'phase',
  'question',
  'revealed',
  'roomSeesCounts',
  'round',
  'rounds',
  'seq',
  'settings',
  'slide',
];
const PICKS = ['one', 'upTo'];
const RESULTS = ['live', 'afterAnswer', 'afterClose'];
const PHASES = ['draft', 'open', 'closed'];

describe('poll-activity.json', () => {
  it.each(['draft', 'open', 'closed', 'answerShown'] as const)(
    'the %s sample round-trips',
    (sample) => {
      const activity = pollActivity(sample);
      expect(activity).toEqual(pollActivityJson[sample]);
      expect(keys(activity)).toEqual(ACTIVITY_KEYS);
      expect(activity.type).toBe('poll');
      expect(PICKS).toContain(activity.pick);
      expect(RESULTS).toContain(activity.results);
      expect(PHASES).toContain(activity.phase);
    },
  );

  it('has no round in draft and no correct answer until it is shown', () => {
    expect(pollActivity('draft').round).toBeNull();
    expect(pollActivity('draft').phase).toBe('draft');
    expect(pollActivity('closed').correct).toBeNull();
    expect(pollActivity('answerShown').correct).toBe(2);
  });

  it('never carries counts or the settings a guest does not need', () => {
    const text = JSON.stringify(pollActivityJson);
    for (const word of ['counts', 'opens', 'privacy', 'settings', 'picks'])
      expect(text).not.toContain(`"${word}"`);
  });
});

describe('guest-poll.json', () => {
  it.each(['answered', 'hidden', 'notAnswered', 'noPoll', 'notOpen', 'joinedLate'] as const)(
    'the %s sample round-trips',
    (sample) => {
      const view = guestPoll(sample);
      expect(view).toEqual(guestPollJson[sample]);
      expect(keys(view)).toEqual(['activity', 'joinedLate', 'mine', 'tally']);
      if (view.activity) expect(keys(view.activity)).toEqual(ACTIVITY_KEYS);
      if (view.mine) expect(keys(view.mine)).toEqual(['picks', 'revision']);
      if (view.tally) expect(keys(view.tally)).toEqual(['answered', 'counts', 'round', 'type']);
    },
  );

  it('hides the counts but not the answer count from a guest who may not see them', () => {
    expect(guestPoll('hidden').tally).toEqual(
      expect.objectContaining({ counts: null, answered: 21 }),
    );
    expect(guestPoll('notAnswered').tally?.counts).toBeNull();
    expect(guestPoll('answered').tally?.counts).toEqual([12, 25, 14, 6]);
    expect(guestPoll('noPoll')).toEqual({
      activity: null,
      mine: null,
      tally: null,
      joinedLate: false,
    });
  });

  it('marks only a guest who joined after the round closed, with no answer, as joined late', () => {
    const late = guestPoll('joinedLate');
    expect(late.joinedLate).toBe(true);
    expect(late.activity?.phase).toBe('closed');
    expect(late.mine).toBeNull();
    for (const sample of ['answered', 'hidden', 'notAnswered', 'noPoll', 'notOpen'] as const)
      expect(guestPoll(sample).joinedLate).toBe(false);
    expect(answerStale().current.joinedLate).toBe(false);
  });

  it('shows a manual poll before it opens with no round, no answer and no tally', () => {
    const view = guestPoll('notOpen');
    expect(view.activity).toEqual(pollActivity('draft'));
    expect(view.activity?.phase).toBe('draft');
    expect(view.activity?.round).toBeNull();
    expect(view.mine).toBeNull();
    expect(view.tally).toBeNull();
  });
});

describe('poll-answer.json', () => {
  it('round-trips the request, the 200 and the 409', () => {
    expect(answerRequest()).toEqual(pollAnswerJson.request);
    expect(keys(answerRequest())).toEqual(['picks', 'revision', 'round']);
    expect(answerAccepted()).toEqual(pollAnswerJson.accepted);
    expect(keys(answerAccepted())).toEqual(['picks', 'revision']);
    expect(answerStale()).toEqual(pollAnswerJson.stale);
    expect(answerStale().error).toBe('stale');
    expect(answerStale().current).toEqual(guestPoll('answered'));
  });

  it('holds each error body of V5', () => {
    expect(answerInvalid().map((body) => body.error)).toEqual([
      'picks: pick one',
      'picks: at most 2',
      'picks: unknown option 7',
      'picks: duplicate',
      'picks: required',
    ]);
    expect(answerNoGuest()).toEqual({ error: 'no_guest' });
    expect(answerNoSession()).toEqual({ error: 'no_session' });
    expect(answerTooMany()).toEqual({ error: 'too_many_answers' });
  });
});

describe('poll-control.json', () => {
  it.each(['draft', 'open', 'closed'] as const)('the %s sample round-trips', (sample) => {
    const view = pollControlView(sample);
    expect(view).toEqual(pollControlJson[sample]);
    expect(keys(view)).toEqual(CONTROL_KEYS);
    expect(keys(view.settings)).toEqual(SETTINGS_KEYS);
    expect(view.phase).toBe(sample);
    expect(view.counts).toHaveLength(view.options.length);
    for (const round of view.rounds) expect(keys(round)).toEqual(['answered', 'joined', 'number']);
  });

  it('has no round id in draft', () => {
    expect(pollControlView('draft').round).toBeNull();
    expect(pollControlView('open').round).not.toBeNull();
  });

  it('holds one 409 body per refusal code, in the order of V4', () => {
    expect(pollRefusals()).toEqual(pollControlJson.refusals);
    expect(pollRefusals().map((body) => body.error)).toEqual([...POLL_REFUSALS]);
  });
});

describe('poll-results.json', () => {
  it('round-trips', () => {
    const results = pollResults();
    expect(results).toEqual(pollResultsJson);
    expect(keys(results)).toEqual(['options', 'question', 'sessions']);
    expect(keys(results.sessions[0])).toEqual(['rounds', 'startedAt']);
    expect(keys(results.sessions[0].rounds[0])).toEqual([
      'answered',
      'counts',
      'joined',
      'number',
      'open',
      'options',
      'rate',
    ]);
  });

  it('gives every round its own options, with one count for each', () => {
    const results = pollResults();
    for (const round of results.sessions[0].rounds)
      expect(round.counts).toHaveLength(round.options.length);
    expect(results.sessions[0].rounds[0].options).not.toEqual(results.options);
  });

  it('sends totals only, never who picked what', () => {
    const text = JSON.stringify(pollResultsJson);
    for (const word of ['picks', 'guest', 'guestId', 'mine'])
      expect(text).not.toContain(`"${word}"`);
  });
});

describe('the poll events in audience-events.json', () => {
  it('round-trips pollTally and pollControl', () => {
    expect(pollTally()).toEqual(audienceEventsJson.pollTally);
    expect(keys(pollTally())).toEqual(['answered', 'counts', 'round', 'type']);
    expect(pollControl()).toEqual(audienceEventsJson.pollControl);
    expect(keys(pollControl())).toEqual(['control', 'type']);
    expect(pollControl().control).toEqual(pollControlView('closed'));
  });

  it('hands out copies a test may change', () => {
    pollTally().answered = 999;
    pollControlView('open').seq = 999;
    expect(pollTally().answered).toBe(audienceEventsJson.pollTally.answered);
    expect(pollControlView('open').seq).toBe(pollControlJson.open.seq);
  });
});

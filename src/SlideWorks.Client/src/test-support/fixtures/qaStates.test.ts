import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestQuestionsJson from '@contracts/screens/guest-questions.json' with { type: 'json' };
import qaControlJson from '@contracts/screens/qa-control.json' with { type: 'json' };
import qaRoomJson from '@contracts/screens/qa-room.json' with { type: 'json' };
import questionAskJson from '@contracts/screens/question-ask.json' with { type: 'json' };
import questionVotesJson from '@contracts/screens/question-votes.json' with { type: 'json' };
import questionsReportJson from '@contracts/screens/questions-report.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { REACTION_EMOJI } from '../../qa/emoji';
import { ASK_REFUSALS, QA_REFUSALS } from '../../qa/types';
import {
  askedQuestion,
  askInvalid,
  askRefusals,
  askRequest,
  guestQaErrors,
  guestQuestion,
  guestQuestions,
  qaControlView,
  qaRefusals,
  qaRoom,
  qaSettingsRequest,
  qaSlideRequest,
  questionControl,
  questionsReport,
  questionTally,
  reactionInvalid,
  reactionRequest,
  reactionsBurst,
  reactionsOff,
  tooManyQuestions,
  tooManyReactions,
  tooManyVotes,
  voteRequest,
  votesAccepted,
  votesInvalid,
  votesRefusals,
  votesStale,
} from './qaStates';

const keys = (value: object) => Object.keys(value).sort();
const ROOM_KEYS = ['listRevision', 'names', 'questions', 'reactions', 'round'];
const GUEST_QUESTION_KEYS = [
  'age',
  'name',
  'number',
  'onScreen',
  'state',
  'text',
  'votes',
  'yours',
];
const VIEW_KEYS = ['mine', 'questions', 'room'];
const MINE_KEYS = ['picks', 'revision'];
const SETTINGS_KEYS = ['names', 'questions', 'reactions', 'review', 'showOn'];
const SWITCH_KEYS = ['questions', 'reactions', 'slide'];
const QA_QUESTION_KEYS = ['age', 'hidden', 'name', 'number', 'onScreen', 'state', 'text', 'votes'];
const CONTROL_KEYS = [
  'counts',
  'here',
  'live',
  'onScreen',
  'questions',
  'reactions',
  'round',
  'seq',
  'settings',
  'slides',
];
const ROOM_QUESTIONS = ['open', 'closed', 'offHere', 'off'];
const STATUSES = ['open', 'answered', 'waiting', 'hidden'];
const STORED_STATES = ['waiting', 'open', 'answered'];

describe('qa-room.json', () => {
  it.each(['open', 'closed', 'offHere', 'off'] as const)('the %s sample round-trips', (sample) => {
    const room = qaRoom(sample);
    expect(room).toEqual(qaRoomJson[sample]);
    expect(keys(room)).toEqual(ROOM_KEYS);
    expect(room.questions).toBe(sample);
    expect(ROOM_QUESTIONS).toContain(room.questions);
  });

  it('has no round and list revision 0 when questions are off, and a round otherwise', () => {
    expect(qaRoom('off').round).toBeNull();
    expect(qaRoom('off').listRevision).toBe(0);
    for (const sample of ['open', 'closed', 'offHere'] as const) {
      expect(qaRoom(sample).round).not.toBeNull();
      expect(qaRoom(sample).listRevision).toBeGreaterThan(0);
    }
  });
});

describe('guest-questions.json', () => {
  it.each(['view', 'ownHidden'] as const)('the %s sample round-trips', (sample) => {
    const view = guestQuestions(sample);
    expect(view).toEqual(guestQuestionsJson[sample]);
    expect(keys(view)).toEqual(VIEW_KEYS);
    expect(keys(view.room)).toEqual(ROOM_KEYS);
    expect(keys(view.mine)).toEqual(MINE_KEYS);
    for (const question of view.questions) {
      expect(keys(question)).toEqual(GUEST_QUESTION_KEYS);
      expect(STATUSES).toContain(question.state);
    }
  });

  it('lists questions in number order, and shows waiting or hidden only on the guest’s own', () => {
    for (const sample of ['view', 'ownHidden'] as const) {
      const numbers = guestQuestions(sample).questions.map((q) => q.number);
      expect(numbers).toEqual([...numbers].sort((a, b) => a - b));
      for (const question of guestQuestions(sample).questions)
        if (question.state === 'waiting' || question.state === 'hidden')
          expect(question.yours).toBe(true);
    }
    expect(guestQuestion(4).state).toBe('waiting');
    expect(guestQuestions('ownHidden').questions.find((q) => q.number === 4)?.state).toBe('hidden');
    expect(guestQuestion(2, { votes: 99 })).toEqual(
      expect.objectContaining({ number: 2, onScreen: true, votes: 99 }),
    );
  });

  it('never says which guest asked or upvoted', () => {
    const text = JSON.stringify(guestQuestionsJson);
    for (const word of ['guestId', 'guest', 'voters', 'askedBy'])
      expect(text).not.toContain(`"${word}"`);
  });

  it('holds the guest errors and the reaction route’s bodies', () => {
    expect(guestQaErrors()).toEqual([
      { error: 'no_guest' },
      { error: 'no_session' },
      { error: 'too_many_attempts' },
    ]);
    expect(reactionRequest()).toEqual({ emoji: 'clap' });
    expect(REACTION_EMOJI).toContain(reactionRequest().emoji);
    expect(reactionInvalid()).toEqual({ error: 'emoji: unknown' });
    expect(reactionsOff()).toEqual({ error: 'reactions_off' });
    expect(tooManyReactions()).toEqual({ error: 'too_many_reactions' });
  });
});

describe('question-ask.json', () => {
  it('round-trips the request and the 201', () => {
    expect(askRequest()).toEqual(questionAskJson.request);
    expect(keys(askRequest())).toEqual(['name', 'text']);
    const created = askedQuestion();
    expect(created).toEqual(questionAskJson.created);
    expect(keys(created)).toEqual(['mine', 'question']);
    expect(keys(created.question)).toEqual(GUEST_QUESTION_KEYS);
    expect(keys(created.mine)).toEqual(MINE_KEYS);
    expect(created.question.yours).toBe(true);
    expect(created.mine.picks).toContain(created.question.number);
  });

  it('holds each error body of Q4', () => {
    expect(askInvalid().map((body) => body.error)).toEqual([
      'text: required',
      'text: at most 200',
      'name: at most 30',
      'name: not allowed',
    ]);
    expect(askRefusals().map((body) => body.error)).toEqual([...ASK_REFUSALS]);
    expect(tooManyQuestions()).toEqual({ error: 'too_many_questions', retryAfter: 42 });
    expect(keys(tooManyQuestions())).toEqual(['error', 'retryAfter']);
  });
});

describe('question-votes.json', () => {
  it('round-trips the request, the 200 and the 409', () => {
    expect(voteRequest()).toEqual(questionVotesJson.request);
    expect(keys(voteRequest())).toEqual(['picks', 'revision', 'round']);
    expect(votesAccepted()).toEqual(questionVotesJson.accepted);
    expect(keys(votesAccepted())).toEqual(MINE_KEYS);
    expect(votesStale()).toEqual(questionVotesJson.stale);
    expect(keys(votesStale())).toEqual(['current', 'error']);
    expect(votesStale().error).toBe('stale');
    expect(votesStale().current).toEqual(guestQuestions('view'));
  });

  it('holds each error body of Q4', () => {
    expect(votesInvalid().map((body) => body.error)).toEqual([
      'picks: unknown question 7',
      'picks: duplicate',
    ]);
    expect(votesRefusals()).toEqual([{ error: 'questions_off' }]);
    expect(tooManyVotes()).toEqual({ error: 'too_many_votes' });
  });
});

describe('qa-control.json', () => {
  it.each(['live', 'notLive'] as const)('the %s sample round-trips', (sample) => {
    const view = qaControlView(sample);
    expect(view).toEqual(qaControlJson[sample]);
    expect(keys(view)).toEqual(CONTROL_KEYS);
    expect(keys(view.settings)).toEqual(SETTINGS_KEYS);
    expect(keys(view.counts)).toEqual(['answered', 'hidden', 'open', 'waiting']);
    expect(keys(view.reactions)).toEqual([...REACTION_EMOJI].sort());
    if (view.here) expect(keys(view.here)).toEqual(SWITCH_KEYS);
    for (const slide of view.slides) expect(keys(slide)).toEqual(SWITCH_KEYS);
    for (const question of view.questions) {
      expect(keys(question)).toEqual(QA_QUESTION_KEYS);
      expect(STORED_STATES).toContain(question.state);
    }
  });

  it('has no round, switch, questions or counts when not live', () => {
    const view = qaControlView('notLive');
    expect(view.live).toBe(false);
    expect(view.round).toBeNull();
    expect(view.here).toBeNull();
    expect(view.onScreen).toBeNull();
    expect(view.questions).toEqual([]);
    expect(view.counts).toEqual({ open: 0, waiting: 0, answered: 0, hidden: 0 });
  });

  it('marks the one question on screen and keeps hidden beside the state', () => {
    const view = qaControlView('live');
    expect(view.questions.filter((q) => q.onScreen).map((q) => q.number)).toEqual([view.onScreen]);
    expect(view.questions.find((q) => q.number === 5)).toEqual(
      expect.objectContaining({ state: 'open', hidden: true }),
    );
  });

  it('round-trips the settings and slide requests and holds one 409 body per refusal code', () => {
    expect(qaSettingsRequest()).toEqual(qaControlJson.settingsRequest);
    expect(keys(qaSettingsRequest())).toEqual(SETTINGS_KEYS);
    expect(qaSlideRequest()).toEqual(qaControlJson.slideRequest);
    expect(keys(qaSlideRequest())).toEqual(['questions', 'reactions']);
    expect(qaRefusals().map((body) => body.error)).toEqual([...QA_REFUSALS]);
  });
});

describe('questions-report.json', () => {
  it('round-trips', () => {
    const report = questionsReport();
    expect(report).toEqual(questionsReportJson);
    expect(keys(report)).toEqual(['sessions']);
    for (const session of report.sessions) {
      expect(keys(session)).toEqual(['answered', 'asked', 'hidden', 'questions', 'startedAt']);
      for (const question of session.questions) {
        expect(keys(question)).toEqual(['askedAt', 'name', 'state', 'text', 'votes']);
        expect(STATUSES).toContain(question.state);
      }
    }
  });

  it('never says which guest asked or upvoted', () => {
    const text = JSON.stringify(questionsReportJson);
    for (const word of ['guestId', 'guest', 'picks', 'mine', 'yours'])
      expect(text).not.toContain(`"${word}"`);
  });
});

describe('the Q&A events in audience-events.json', () => {
  it('round-trips questionTally, questionControl and reactions', () => {
    expect(questionTally()).toEqual(audienceEventsJson.questionTally);
    expect(keys(questionTally())).toEqual(['listRevision', 'round', 'type', 'votes']);
    for (const votes of questionTally().votes) expect(keys(votes)).toEqual(['number', 'votes']);
    expect(questionControl()).toEqual(audienceEventsJson.questionControl);
    expect(keys(questionControl())).toEqual(['control', 'type']);
    expect(questionControl().control).toEqual(qaControlView('live'));
    expect(reactionsBurst()).toEqual(audienceEventsJson.reactions);
    expect(keys(reactionsBurst())).toEqual(['counts', 'slide', 'type']);
    expect(keys(reactionsBurst().counts)).toEqual([...REACTION_EMOJI].sort());
  });

  it('tallies only the questions another guest may see', () => {
    const visible = guestQuestions('view')
      .questions.filter((q) => q.state === 'open' || q.state === 'answered')
      .map((q) => q.number);
    expect(questionTally().votes.map((v) => v.number)).toEqual(visible);
  });

  it('carries the room on the audience state', () => {
    expect(audienceEventsJson.audienceState.room).toEqual(qaRoom('open'));
  });

  it('hands out copies a test may change', () => {
    questionTally().listRevision = 999;
    qaControlView('live').seq = 999;
    qaRoom().listRevision = 999;
    expect(questionTally().listRevision).toBe(audienceEventsJson.questionTally.listRevision);
    expect(qaControlView('live').seq).toBe(qaControlJson.live.seq);
    expect(qaRoom().listRevision).toBe(qaRoomJson.open.listRevision);
  });
});

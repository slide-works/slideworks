import { describe, expect, it } from 'vitest';
import { questionErrorText } from './questionErrorText';

const SAM = { presenter: 'Sam' };
const UNKNOWN = 'Something went wrong on our side. Try again in a moment.';
const NETWORK = 'Your phone couldn’t reach the talk. Check your connection, then try again.';

describe('questionErrorText', () => {
  it.each([
    ['questions_closed', 'Sam has closed questions. You can still upvote.'],
    ['questions_off_here', 'Questions are off on this slide. You can still upvote.'],
    ['questions_off', 'Sam has turned questions off.'],
    ['too_many_questions', 'You’ve asked a lot in the last minute. Wait a moment, then ask again.'],
    [
      'too_many_votes',
      'You’ve changed your upvotes a lot in the last minute. Wait a moment, then try again.',
    ],
    [
      'stale',
      'The questions changed before your upvote was sent. Check the list and upvote again.',
    ],
    ['reactions_off', 'Reactions are off right now.'],
    ['too_many_reactions', 'That’s a lot of reactions. Wait a moment, then react again.'],
    [
      'too_many_attempts',
      'Too many tries from this network right now. Wait a moment, then try again.',
    ],
    ['no_session', 'This talk isn’t live any more.'],
    [
      'no_guest',
      'Your browser isn’t keeping this site’s cookie. Allow cookies for this site, then reload the page.',
    ],
    ['text: required', 'Type a question first.'],
    ['text: at most 200', 'Keep your question to 200 characters.'],
    ['name: at most 30', 'Keep your name to 30 characters.'],
    ['name: not allowed', 'Sam has turned names off. Send your question without one.'],
    [
      'picks: unknown question 7',
      'That question isn’t on the list any more. Check the list and upvote again.',
    ],
  ])('gives the guest a sentence for %s', (error, text) => {
    expect(questionErrorText(error, SAM)).toBe(text);
  });

  it('names the presenter it is given', () => {
    expect(questionErrorText('questions_closed', { presenter: 'Ana' })).toBe(
      'Ana has closed questions. You can still upvote.',
    );
  });

  it('counts down too_many_questions while retryIn is above 0', () => {
    expect(questionErrorText('too_many_questions', { presenter: 'Sam', retryIn: 42 })).toBe(
      'You can ask again in 42 s',
    );
  });

  it('passes a sentence the client wrote through as it is', () => {
    expect(questionErrorText(NETWORK, SAM)).toBe(NETWORK);
  });

  it.each([
    'Request failed (400)',
    'Request failed (413)',
    'some_new_code',
    'picks: duplicate',
    'picks: at most 1000',
    'emoji: unknown',
  ])('never shows a code or rule it does not know: %s', (error) => {
    expect(questionErrorText(error, SAM)).toBe(UNKNOWN);
  });
});

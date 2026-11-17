import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestJson from '@contracts/screens/guest.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { LAYOUT_NAMES } from '../../slides/slots';
import { audienceEnded, audienceState, guestView, joinedCount } from './guestStates';

const keys = (value: object) => Object.keys(value).sort();
const STATE_KEYS = ['activity', 'current', 'phase', 'revision', 'seq', 'type'];

describe('guest.json', () => {
  it.each(['live', 'lobby', 'ended'] as const)('the %s sample round-trips', (phase) => {
    const view = guestView(phase);
    expect(view).toEqual(guestJson[phase]);
    expect(keys(view)).toEqual(['deckTitle', 'deckUrl', 'look', 'presenterName', 'state']);
    expect(keys(view.state)).toEqual(STATE_KEYS);
    expect(view.state.type).toBe('audienceState');
    expect(view.state.phase).toBe(phase);
    expect(view.state.activity).toBeNull();
  });

  it('carries the slide on screen only while live', () => {
    const current = guestView('live').state.current!;
    expect(keys(current)).toEqual(['black', 'count', 'index', 'layout', 'slots', 'step', 'steps']);
    expect(LAYOUT_NAMES).toContain(current.layout);
    expect(guestView('lobby').state.current).toBeNull();
    expect(guestView('ended').state.current).toBeNull();
  });

  it('has a deck URL only when the deck is shared by link', () => {
    expect(guestView('live').deckUrl).toBe(
      'https://slideworks.example/d/7Kq2xYp9LmQa/ship-smaller',
    );
    expect(guestView('lobby').deckUrl).toBeNull();
  });

  it('sends no notes, viewers or timer anywhere', () => {
    const text = JSON.stringify(guestJson);
    for (const word of ['notes', 'viewers', 'timer', 'email', 'skipDeepDives', 'deepDive'])
      expect(text).not.toContain(`"${word}"`);
  });
});

describe('audience-events.json', () => {
  it('has one sample per event type, keyed by its type', () => {
    expect(keys(audienceEventsJson)).toEqual([
      'audienceEnded',
      'audienceState',
      'joinedCount',
      'pollControl',
      'pollTally',
    ]);
    for (const [name, sample] of Object.entries(audienceEventsJson)) expect(sample.type).toBe(name);
  });

  it('round-trips each event', () => {
    expect(audienceState()).toEqual(audienceEventsJson.audienceState);
    expect(audienceEnded()).toEqual(audienceEventsJson.audienceEnded);
    expect(joinedCount()).toEqual(audienceEventsJson.joinedCount);
    expect(keys(audienceState())).toEqual(STATE_KEYS);
    expect(keys(joinedCount())).toEqual(['count', 'type']);
  });

  it('hands out copies a test may change', () => {
    audienceState().seq = 999;
    expect(audienceState().seq).toBe(audienceEventsJson.audienceState.seq);
  });
});

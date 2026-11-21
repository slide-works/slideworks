import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestJson from '@contracts/screens/guest.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { LAYOUT_NAMES, SLOT_KEYS } from '../../slides/slots';
import { REQUIRED_SLOT_KEYS } from './layoutSlots';
import { audienceEnded, audienceState, guestView, joinedCount } from './guestStates';

const keys = (value: object) => Object.keys(value).sort();
const STATE_KEYS = ['activity', 'current', 'phase', 'revision', 'room', 'seq', 'type'];

describe('guest.json', () => {
  it.each(['live', 'lobby', 'ended'] as const)(
    'the %s sample has the guest page shape',
    (phase) => {
      const view = guestView(phase);
      expect(keys(view)).toEqual(['deckTitle', 'deckUrl', 'look', 'presenterName', 'state']);
      expect(keys(view.state)).toEqual(STATE_KEYS);
      expect(view.state.type).toBe('audienceState');
      expect(view.state.phase).toBe(phase);
      expect(view.state.activity).toBeNull();
    },
  );

  it('carries the slide on screen only while live', () => {
    const current = guestView('live').state.current!;
    expect(keys(current)).toEqual([
      'black',
      'count',
      'index',
      'layout',
      'slots',
      'step',
      'steps',
      'surface',
    ]);
    expect(LAYOUT_NAMES).toContain(current.layout);
    expect(guestView('lobby').state.current).toBeNull();
    expect(guestView('ended').state.current).toBeNull();
  });

  it("the live sample's slots are valid for its layout", () => {
    const { layout, slots } = guestView('live').state.current!;
    const allowed: readonly string[] = SLOT_KEYS[layout];
    const given = Object.keys(slots);
    for (const key of given) expect(allowed, `slot ${key}`).toContain(key);
    for (const key of REQUIRED_SLOT_KEYS[layout]) expect(given, `required ${key}`).toContain(key);
  });

  it('hands out a fresh copy on every call', () => {
    const first = guestView('live');
    first.deckTitle = 'changed';
    (first.state.current!.slots as { headline?: string }).headline = 'changed';
    const second = guestView('live');
    expect(second.deckTitle).toBe(guestJson.live.deckTitle);
    expect(second.state.current!.slots).toEqual(guestJson.live.state.current!.slots);
  });

  it('carries the room only while live, and drops it on request', () => {
    expect(guestView('live').state.room?.questions).toBe('open');
    expect(guestView('lobby').state.room).toBeNull();
    expect(guestView('ended').state.room).toBeNull();
    const noRoom = guestView('live', { room: null });
    expect(noRoom.state.room).toBeNull();
    expect({ ...noRoom.state, room: guestJson.live.state.room }).toEqual(guestJson.live.state);
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
      'openText',
      'pollControl',
      'pollTally',
      'questionControl',
      'questionTally',
      'reactions',
      'wordCloud',
    ]);
    for (const [name, sample] of Object.entries(audienceEventsJson)) expect(sample.type).toBe(name);
  });

  it('has the shape of each event', () => {
    expect(keys(audienceState())).toEqual(STATE_KEYS);
    expect(keys(joinedCount())).toEqual(['count', 'type']);
    expect(audienceEnded().type).toBe('audienceEnded');
  });

  it('hands out copies a test may change', () => {
    audienceState().seq = 999;
    expect(audienceState().seq).toBe(audienceEventsJson.audienceState.seq);
  });
});

import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import guestJson from '@contracts/screens/guest.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { LAYOUT_NAMES, SLOT_KEYS } from '../../slides/slots';
import { REQUIRED_SLOT_KEYS } from './layoutSlots';
import {
  audienceEnded,
  audienceState,
  guestTranslations,
  guestView,
  joinedCount,
  readingIn,
  translationsOff,
} from './guestStates';

const keys = (value: object) => Object.keys(value).sort();
const STATE_KEYS = ['activity', 'current', 'language', 'phase', 'revision', 'room', 'seq', 'type'];

describe('guest.json', () => {
  it.each(['live', 'lobby', 'ended'] as const)(
    'the %s sample has the guest page shape',
    (phase) => {
      const view = guestView(phase);
      expect(keys(view)).toEqual([
        'deckTitle',
        'deckUrl',
        'look',
        'presenterName',
        'state',
        'translations',
      ]);
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

  it('offers languages only while live, primary first', () => {
    const translations = guestView('live').translations!;
    expect(keys(translations)).toEqual(['languages', 'primary']);
    expect(translations.primary).toBe('en');
    expect(translations.languages.map((l) => l.code)).toEqual(['en', 'es', 'th', 'ja']);
    for (const language of translations.languages)
      expect(keys(language)).toEqual(['code', 'complete', 'englishName', 'name', 'short']);
    expect(translations.languages[2]).toEqual({
      code: 'th',
      name: 'ไทย',
      englishName: 'Thai',
      short: 'ไทย',
      complete: 83,
    });
    expect(guestTranslations()).toEqual(translations);
    expect(guestView('lobby').translations).toBeNull();
    expect(guestView('ended').translations).toBeNull();
  });

  it('composes the live state in a language, and none while translations are off', () => {
    expect(guestView('live').state.language).toEqual({ code: 'th', fallbackLines: 2 });
    expect(guestView('lobby').state.language).toBeNull();
    expect(
      guestView('live', { language: { code: 'ja', fallbackLines: 0 } }).state.language,
    ).toEqual({
      code: 'ja',
      fallbackLines: 0,
    });
    const off = translationsOff();
    expect(off.translations).toBeNull();
    expect(off.state.language).toBeNull();
    expect(off.state.current).toEqual(guestView('live').state.current);
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
      'readingIn',
      'wordCloud',
    ]);
    for (const [name, sample] of Object.entries(audienceEventsJson)) expect(sample.type).toBe(name);
  });

  it('has the shape of each event', () => {
    expect(keys(audienceState())).toEqual(STATE_KEYS);
    expect(keys(joinedCount())).toEqual(['count', 'type']);
    expect(keys(readingIn())).toEqual(['counts', 'type']);
    expect(readingIn().counts?.map((count) => keys(count))).toEqual(
      Array(4).fill(['code', 'count']),
    );
    expect(audienceEnded().type).toBe('audienceEnded');
  });

  it('hands out copies a test may change', () => {
    audienceState().seq = 999;
    expect(audienceState().seq).toBe(audienceEventsJson.audienceState.seq);
    guestTranslations().languages[0].complete = 1;
    expect(guestTranslations().languages[0].complete).toBe(100);
  });

  it('composes the state in the guest language', () => {
    expect(audienceState().language).toEqual({ code: 'th', fallbackLines: 2 });
  });
});

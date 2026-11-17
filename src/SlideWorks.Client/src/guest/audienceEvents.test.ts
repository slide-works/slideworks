import audienceEventsJson from '@contracts/screens/audience-events.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { isAudienceEvent } from './audienceEvents';

describe('isAudienceEvent', () => {
  it.each(Object.keys(audienceEventsJson) as (keyof typeof audienceEventsJson)[])(
    'accepts the %s sample',
    (name) => {
      expect(isAudienceEvent(audienceEventsJson[name])).toBe(true);
    },
  );

  it.each(['questionTally', 'questionControl', 'reactions'])('accepts a %s event', (type) => {
    expect(isAudienceEvent({ type })).toBe(true);
  });

  it.each([
    ['an unknown type', { type: 'questionAsked' }],
    ['a type in the wrong case', { type: 'Reactions' }],
    ['no type', { slide: 5 }],
    ['null', null],
    ['a string', 'reactions'],
  ])('refuses %s', (_, value) => {
    expect(isAudienceEvent(value)).toBe(false);
  });
});

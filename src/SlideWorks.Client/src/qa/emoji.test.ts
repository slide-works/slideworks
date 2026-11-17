import qaControlJson from '@contracts/screens/qa-control.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { REACTION_EMOJI, REACTIONS, reactionOf } from './emoji';

describe('the five reactions', () => {
  it('are the keys of the wire counts, in display order', () => {
    expect(REACTION_EMOJI).toEqual(['thumbsUp', 'heart', 'laugh', 'wow', 'clap']);
    expect([...REACTION_EMOJI].sort()).toEqual(Object.keys(qaControlJson.live.reactions).sort());
    expect(REACTIONS.map((reaction) => reaction.key)).toEqual(REACTION_EMOJI);
  });

  it('each have an emoji and a name of their own', () => {
    expect(REACTIONS.map((reaction) => reaction.emoji)).toEqual(['👍', '❤️', '😂', '😮', '👏']);
    expect(REACTIONS.map((reaction) => reaction.name)).toEqual([
      'thumbs up',
      'heart',
      'laugh',
      'wow',
      'clap',
    ]);
    expect(reactionOf('clap')).toEqual({ key: 'clap', emoji: '👏', name: 'clap' });
  });
});

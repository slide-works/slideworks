import type { ReactionEmoji } from './types';

// The five reactions (D6), in the order the phone's row and the presenter's counter show them. `key` is the wire name
// (ReactionRequest.emoji and the keys of ReactionCounts), `emoji` the character drawn, and `name` what a screen reader
// hears ("React thumbs up"). Literals only: no emoji package.
export type Reaction = { key: ReactionEmoji; emoji: string; name: string };

const TABLE: Record<ReactionEmoji, Omit<Reaction, 'key'>> = {
  thumbsUp: { emoji: '👍', name: 'thumbs up' },
  heart: { emoji: '❤️', name: 'heart' },
  laugh: { emoji: '😂', name: 'laugh' },
  wow: { emoji: '😮', name: 'wow' },
  clap: { emoji: '👏', name: 'clap' },
};

// The wire names in display order.
export const REACTION_EMOJI: readonly ReactionEmoji[] = [
  'thumbsUp',
  'heart',
  'laugh',
  'wow',
  'clap',
];

// The reaction for a wire name.
export const reactionOf = (key: ReactionEmoji): Reaction => ({ key, ...TABLE[key] });

export const REACTIONS: readonly Reaction[] = REACTION_EMOJI.map(reactionOf);

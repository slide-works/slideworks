// The inline block types, and the point and column types two layouts share (K5).
// Slot text is plain text: never parse it as Markdown or HTML (D12).
import type { Fill, Tone } from './shared';

// Inline blocks. `chip`, `badge` and `stamp` are plain strings in the slots;
// `journey` is rendered from the deck's parts and is never set by an agent.
export type CalloutBlock = {
  tone: Tone;
  fill?: Fill;
  text: string;
  reveal?: 'with-slide' | 'last';
};

// An uploaded image: `image` is its id (served at /api/images/{id}).
export type ImageRef = { image: string; alt: string };

export type LeadText = { lead: string; text: string };
export type BulletItem = string | LeadText;
export type TitleText = { title: string; text: string };

// Exactly one of items, text or image is set.
export type ColumnSlot = {
  title?: string;
  items?: BulletItem[];
  text?: string;
  image?: ImageRef;
};

// A phrase that is already in a slot's text, drawn in a tone.
export type EmphasisTone = 'accent' | 'positive' | 'negative';
export type Emphasis = { text: string; tone: EmphasisTone; in?: string };

export type PanelFill = 'soft' | 'solid' | 'plain';

export type LevelsBlock = {
  title?: string;
  items: { text: string; mark?: string; state?: 'plain' | 'current' }[];
};

export type FactsBlock = { label: string; value: string; tone?: Tone }[];

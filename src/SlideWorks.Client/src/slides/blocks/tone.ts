import type { CSSProperties } from 'react';
import type { PaletteTokenKey } from '../../api/types';
import type { Fill, Tone } from '../slots';

// The four tones a panel is drawn in (SW-2 L4).
export type PanelTone = 'accent' | 'positive' | 'negative' | 'inverse';

// A toned panel's surface and ink, as palette token names (without --sw-).
export type TonePair = { surface: PaletteTokenKey; ink: PaletteTokenKey };

// good, bad and info are kept in storage for ever (D15); they draw as these.
const CANONICAL: Record<Tone, PanelTone> = {
  accent: 'accent',
  positive: 'positive',
  negative: 'negative',
  inverse: 'inverse',
  good: 'positive',
  bad: 'negative',
  info: 'accent',
};

// soft is the role's own tint and ink; solid is the palette's panel pair.
const SOFT: Record<PanelTone, TonePair> = {
  accent: { surface: 'accent-light', ink: 'accent-dark' },
  positive: { surface: 'good-soft', ink: 'good-ink' },
  negative: { surface: 'bad-soft', ink: 'bad-ink' },
  inverse: { surface: 'night', ink: 'night-ink' },
};

const SOLID: Record<PanelTone, TonePair> = {
  accent: { surface: 'panel-accent', ink: 'panel-accent-ink' },
  positive: { surface: 'panel-positive', ink: 'panel-positive-ink' },
  negative: { surface: 'panel-negative', ink: 'panel-negative-ink' },
  inverse: { surface: 'panel-inverse', ink: 'panel-inverse-ink' },
};

// The tone a panel draws in, with an alias resolved.
export function panelTone(tone: Tone): PanelTone {
  return CANONICAL[tone];
}

// The surface and ink token names for a tone and fill; the fill is soft when
// left out.
export function tonePair(tone: Tone, fill: Fill = 'soft'): TonePair {
  return (fill === 'solid' ? SOLID : SOFT)[panelTone(tone)];
}

// The pair as the --panel-surface and --panel-ink custom properties that the
// toned panels' CSS reads.
export function toneStyle(tone: Tone, fill?: Fill): CSSProperties {
  const { surface, ink } = tonePair(tone, fill);
  return {
    '--panel-surface': `var(--sw-${surface})`,
    '--panel-ink': `var(--sw-${ink})`,
  } as CSSProperties;
}

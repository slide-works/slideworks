import { describe, expect, it } from 'vitest';
import type { Fill, Tone } from '../slots';
import { panelTone, tonePair, toneRule, toneStyle } from './tone';

describe('tonePair', () => {
  it.each<[Tone, Fill, string, string]>([
    ['accent', 'soft', 'accent-light', 'accent-dark'],
    ['positive', 'soft', 'good-soft', 'good-ink'],
    ['negative', 'soft', 'bad-soft', 'bad-ink'],
    ['inverse', 'soft', 'night', 'night-ink'],
    ['accent', 'solid', 'panel-accent', 'panel-accent-ink'],
    ['positive', 'solid', 'panel-positive', 'panel-positive-ink'],
    ['negative', 'solid', 'panel-negative', 'panel-negative-ink'],
    ['inverse', 'solid', 'panel-inverse', 'panel-inverse-ink'],
  ])('%s %s is %s with %s', (tone, fill, surface, ink) => {
    expect(tonePair(tone, fill)).toEqual({ surface, ink });
  });

  it.each<[Tone, Tone]>([
    ['good', 'positive'],
    ['bad', 'negative'],
    ['info', 'accent'],
  ])('the alias %s draws as %s in both fills', (alias, tone) => {
    expect(panelTone(alias)).toBe(tone);
    expect(tonePair(alias, 'soft')).toEqual(tonePair(tone, 'soft'));
    expect(tonePair(alias, 'solid')).toEqual(tonePair(tone, 'solid'));
  });

  it.each<Tone>(['accent', 'positive', 'negative', 'inverse', 'good', 'bad', 'info'])(
    'plain keeps the card surface for %s',
    (tone) => {
      expect(tonePair(tone, 'plain')).toEqual({ surface: 'card', ink: 'ink' });
    },
  );

  it('defaults the fill to soft', () => {
    expect(tonePair('negative')).toEqual(tonePair('negative', 'soft'));
    expect(tonePair('info')).toEqual({ surface: 'accent-light', ink: 'accent-dark' });
  });
});

describe('toneStyle', () => {
  it('sets the panel surface and ink to the pair tokens', () => {
    expect(toneStyle('good', 'solid')).toEqual({
      '--panel-surface': 'var(--sw-panel-positive)',
      '--panel-ink': 'var(--sw-panel-positive-ink)',
    });
    expect(toneStyle('accent')).toEqual({
      '--panel-surface': 'var(--sw-accent-light)',
      '--panel-ink': 'var(--sw-accent-dark)',
    });
  });

  it('plain keeps the card surface', () => {
    expect(toneStyle('negative', 'plain')).toEqual({
      '--panel-surface': 'var(--sw-card)',
      '--panel-ink': 'var(--sw-ink)',
    });
  });
});

describe('toneRule', () => {
  it.each<[Tone, string]>([
    ['accent', 'accent'],
    ['positive', 'good'],
    ['negative', 'bad'],
    ['inverse', 'night'],
    ['info', 'accent'],
    ['good', 'good'],
    ['bad', 'bad'],
  ])("%s's rule is %s", (tone, token) => {
    expect(toneRule(tone)).toBe(token);
  });
});

import { describe, expect, it } from 'vitest';
import palettes from '@contracts/palettes.json';
import {
  CONTRAST_PAIRS,
  checkPairs,
  contrastRatio,
  fixFor,
  isHexColour,
  mixColours,
  nearestPassing,
  ratioText,
} from './contrast';

const cobalt = palettes.palettes.find((palette) => palette.isDefault)!.tokens as Record<
  string,
  string
>;

describe('contrast', () => {
  it("matches the server's ratio for the sample pairs", () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(contrastRatio('#ffffff', '#ffffff')).toBeCloseTo(1, 5);
    // Contrast.cs: (0.2126 L(r) + 0.7152 L(g) + 0.0722 L(b) + 0.05) ratio; #777777 on white is 4.48.
    expect(contrastRatio('#777777', '#ffffff')).toBeCloseTo(4.4783, 3);
    expect(ratioText(4.4783)).toBe('4.4:1');
    expect(ratioText(3.6)).toBe('3.6:1');
    // Every built-in palette passes every pair, as the server holds it to.
    for (const palette of palettes.palettes) {
      const checks = checkPairs(palette.tokens);
      expect(checks).toHaveLength(CONTRAST_PAIRS.length);
      expect(checks.filter((check) => !check.pass).map((check) => check.pair.name)).toEqual([]);
    }
  });

  it('mixes the ground of a pair with over and share', () => {
    expect(mixColours('#ffffff', '#000000', 0.5)).toBe('#808080');
    const tokens = {
      ...cobalt,
      'panel-accent-ink': '#ffffff',
      night: '#000000',
      'panel-accent': '#ffffff',
    };
    const mixed = checkPairs({ ...tokens }).find((check) => check.pair.name === 'pin-solid-accent');
    // night mixed at 25% over panel-accent: 25% black over white is #bfbfbf.
    expect(mixed?.ground).toBe('#bfbfbf');
    expect(mixColours('#ff0000', '#0000ff', 0.24)).toBe('#3d00c2');
  });

  it('finds a passing colour', () => {
    const fixed = nearestPassing('#ea580c', '#ffffff', 4.5);
    expect(fixed).not.toBeNull();
    expect(contrastRatio(fixed!, '#ffffff')).toBeGreaterThanOrEqual(4.5);
    expect(nearestPassing('#000000', '#ffffff', 4.5)).toBe('#000000');
    expect(nearestPassing('#777777', '#777777', 21)).toBeNull();
    const failing = { ...cobalt, dd: '#ea580c' };
    const check = checkPairs(failing).find((c) => c.pair.name === 'deep-dive-pill')!;
    expect(check.pass).toBe(false);
    const fix = fixFor(check, failing)!;
    expect(fix.token).toBe('dd');
    expect(isHexColour(fix.colour)).toBe(true);
  });
});

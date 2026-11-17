import { describe, expect, it } from 'vitest';
import { PALETTES, TEMPLATES, lookViewOf } from '../test-support/fixtures/lookViewOf';
import { lookStyle } from './lookStyle';

const styleOf = (template: string, palette: string) =>
  lookStyle(lookViewOf(template, palette)) as Record<string, string | number>;

describe('lookStyle', () => {
  it.each(PALETTES.map((palette) => [palette.name, palette] as const))(
    'sets every %s palette token as a --sw- variable',
    (name, palette) => {
      const style = styleOf('talk', name);
      for (const [key, value] of Object.entries(palette.tokens))
        expect(style[`--sw-${key}`], key).toBe(value);
    },
  );

  it.each(TEMPLATES.map((template) => [template.name, template] as const))(
    'applies the %s template type and shape',
    (name, template) => {
      const style = styleOf(name, 'cobalt');
      expect(style['--sw-font-head']).toBe(
        `'${template.type.head}', '${template.type.body}', system-ui, sans-serif`,
      );
      expect(style['--sw-font-body']).toBe(`'${template.type.body}', system-ui, sans-serif`);
      expect(style['--sw-head-weight']).toBe(template.type.headWeight);
      expect(style['--sw-head-tracking']).toBe(template.type.headTracking);
      expect(style['--sw-radius-sm']).toBe(`${template.shape.radius.sm}px`);
      expect(style['--sw-radius-md']).toBe(`${template.shape.radius.md}px`);
      expect(style['--sw-radius-lg']).toBe(`${template.shape.radius.lg}px`);
      expect(style['--sw-radius-xl']).toBe(`${template.shape.radius.xl}px`);
      expect(style['--sw-lift']).toBe(template.shape.shadow);
    },
  );

  it('sets the derived tokens on the same element, from the accent family and the night glow', () => {
    const style = styleOf('talk', 'ember');
    expect(style['--sw-info']).toBe('var(--sw-accent)');
    expect(style['--sw-info-soft']).toBe('var(--sw-accent-light)');
    expect(style['--sw-info-ink']).toBe('var(--sw-accent-dark)');
    const surface = String(style['--sw-night-surface']);
    expect(surface).toContain('color-mix(in srgb, var(--sw-night-glow) 45%, transparent)');
    expect(surface).toContain('color-mix(in srgb, var(--sw-night-glow) 12%, transparent)');
    expect(surface.endsWith('var(--sw-night)')).toBe(true);
    // No colour of its own: everything comes from the palette's tokens.
    expect(surface).not.toMatch(/#[0-9a-f]{3,8}\b|rgba?\(/i);
  });
});

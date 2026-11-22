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

  it.each(PALETTES.map((palette) => [palette.name, palette] as const))(
    'sets accent-2 and leaves the mono token alone for %s',
    (name, palette) => {
      const style = styleOf('talk', name);
      expect(style['--sw-accent-2']).toBe(palette.tokens.accent);
      // A built-in has no mono font of its own: tokens.css's stack stands.
      expect(style).not.toHaveProperty('--sw-font-mono');
      // The extras belong to a custom look: a built-in leaves them unset.
      expect(Object.keys(style).filter((key) => key.startsWith('--sw-extra-'))).toEqual([]);
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

  const monoFont = (family: string) => ({
    family,
    role: 'mono' as const,
    url: '/api/templates/tpl_x/assets/a1',
    weight: 400,
    style: 'normal' as const,
  });
  const withFonts = (...fonts: ReturnType<typeof monoFont>[]) =>
    lookStyle(lookViewOf('talk', 'cobalt', { fonts })) as Record<string, string>;

  it("sets the template's mono font", () => {
    expect(withFonts(monoFont('Northwind Mono'))['--sw-font-mono']).toBe(
      "'Northwind Mono', ui-monospace, monospace",
    );
  });

  it('uses the first mono family when there are two', () => {
    const style = withFonts(monoFont('First Mono'), monoFont('Second Mono'));
    expect(style['--sw-font-mono']).toBe("'First Mono', ui-monospace, monospace");
  });

  it('drops a quote in a family name', () => {
    const style = withFonts(monoFont('Evil\' "Mono\\'));
    expect(style['--sw-font-mono']).toBe("'Evil Mono', ui-monospace, monospace");
  });

  it('sets a colour and an ink token per extra colour, in order', () => {
    const extras = [
      { name: 'extra-1', colour: '#111111', ink: '#ffffff' },
      { name: 'extra-2', colour: '#222222', ink: '#eeeeee' },
    ];
    const style = lookStyle(lookViewOf('talk', 'cobalt', { extras })) as Record<string, string>;
    expect(Object.keys(style).filter((key) => key.includes('extra'))).toEqual([
      '--sw-extra-1',
      '--sw-extra-1-ink',
      '--sw-extra-2',
      '--sw-extra-2-ink',
    ]);
    expect(style['--sw-extra-2']).toBe('#222222');
    expect(style['--sw-extra-2-ink']).toBe('#eeeeee');
  });

  it('sets no extra token on a built-in', () => {
    const style = styleOf('talk', 'cobalt');
    expect(Object.keys(style).filter((key) => key.includes('extra'))).toEqual([]);
  });
});

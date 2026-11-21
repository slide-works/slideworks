import { describe, expect, it } from 'vitest';
import { looks } from '../test-support/fixtures/screens';
import { DARK_LIFT } from './cardLift';
import { lookOf } from './lookOf';

describe('lookOf', () => {
  it('builds a preview LookView from a looks.json template and palette', () => {
    const [template] = looks.templates;
    const palette = looks.palettes[0];
    const look = lookOf(template, palette, '16 Nov 2026');
    expect(look).toEqual({
      template: {
        name: template.name,
        label: template.label,
        layouts: template.layouts,
        chrome: template.chrome,
        type: template.type,
        shape: template.shape,
        ornament: template.ornament,
        titleArt: template.titleArt,
      },
      palette: { name: palette.name, label: palette.label, tokens: palette.tokens },
      logo: null,
      fonts: [],
      logos: { light: null, dark: null, showOnDark: false },
      art: { kind: 'generator', generator: template.titleArt, url: null },
      ornament: { colours: [] },
      topBar: 'none',
      templateRef: template.ref,
      templateVersion: template.version,
      latestVersion: template.version,
      extras: [],
      customLayouts: [],
      templateMissing: false,
      draft: template.chrome.draft,
      footerDateLabel: '16 Nov 2026',
      joinBar: false,
    });
  });

  it('has no title art when the template names none', () => {
    const [template] = looks.templates;
    const look = lookOf({ ...template, titleArt: null }, looks.palettes[0], 'x');
    expect(look.art).toEqual({ kind: 'none', generator: null, url: null });
  });

  it("lifts cards on a dark palette's paper, as the server's look does (SW-148 D2)", () => {
    const [template] = looks.templates;
    const palette = looks.palettes[0];
    const dark = { ...palette, tokens: { ...palette.tokens, paper: '#0e1424' } };
    expect(lookOf(template, palette, 'x').template.shape.shadow).toBe(template.shape.shadow);
    expect(lookOf(template, dark, 'x').template.shape.shadow).toBe(DARK_LIFT);
  });

  it("takes draft from the template's chrome", () => {
    const palette = looks.palettes[0];
    for (const template of looks.templates)
      expect(lookOf(template, palette, 'x').draft, template.name).toBe(template.chrome.draft);
  });
});

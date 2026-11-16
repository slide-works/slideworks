import { describe, expect, it } from 'vitest';
import { looks } from '../test-support/fixtures/screens';
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
      draft: template.chrome.draft,
      footerDateLabel: '16 Nov 2026',
    });
  });

  it("takes draft from the template's chrome", () => {
    const palette = looks.palettes[0];
    for (const template of looks.templates)
      expect(lookOf(template, palette, 'x').draft, template.name).toBe(template.chrome.draft);
  });
});

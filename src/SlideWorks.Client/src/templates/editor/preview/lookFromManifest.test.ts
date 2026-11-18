import { describe, expect, it } from 'vitest';
import palettesJson from '@contracts/palettes.json' with { type: 'json' };
import { templateEditor } from '../../../test-support/fixtures/templates';
import type { LookPalette } from '../../../api/types';
import { PREVIEW_SLIDES } from '../EditorPreview';
import { lookFromManifest } from './lookFromManifest';

const base = palettesJson.palettes[0] as LookPalette;
const { manifest, assets } = templateEditor;
const without = (value: typeof manifest): typeof manifest => {
  const copy = { ...value };
  delete copy.palette;
  return copy;
};

describe('lookFromManifest', () => {
  it('carries the top bar', () => {
    expect(lookFromManifest(manifest, assets, base).topBar).toBe('accent');
    const off = { ...manifest, chrome: { ...manifest.chrome, topBar: 'none' as const } };
    expect(lookFromManifest(off, assets, base).topBar).toBe('none');
  });

  it('draws three sample slides', () => {
    expect(PREVIEW_SLIDES.map((slide) => slide.layout)).toEqual(['title', 'bullets', 'divider']);
  });

  it('turns an asset id into the url of its asset', () => {
    const look = lookFromManifest(manifest, assets, base);
    const urlOf = (id: string | undefined) => assets.find((a) => a.assetId === id)?.url;
    expect(look.logos.light).toBe(urlOf(manifest.logos.light));
    expect(look.logos.light).not.toBeNull();
    expect(look.fonts.map((font) => font.url)).toEqual(
      manifest.fonts.map((font) => urlOf(font.file)),
    );
  });

  it('leaves out a file the view does not list', () => {
    const look = lookFromManifest(manifest, [], base);
    expect(look.fonts).toEqual([]);
    expect(look.logos.light).toBeNull();
  });

  it('draws in the base palette when the manifest brings none', () => {
    const look = lookFromManifest(without(manifest), assets, base);
    expect(look.palette.tokens).toEqual(base.tokens);
    expect(look.palette.name).toBe(base.name);
  });

  it('uses the shape and type of the manifest', () => {
    const look = lookFromManifest(manifest, assets, base);
    expect(look.template.shape).toEqual(manifest.shape);
    expect(look.template.type).toEqual(manifest.type);
  });
});

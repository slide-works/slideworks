import type { LookFont, LookArt, LookExtra, LookLogos } from '../../../api/look';
import type { LookPalette, LookView, PaletteTokens } from '../../../api/types';
import type { LayoutName } from '../../../slides/slots';
import type { TemplateAsset, TemplateManifest } from '../../types';
import { cardLift } from '../../../look/cardLift';

// The footer date a preview slide shows when the footer is on date: the preview has no clock.
const SAMPLE_DATE = 'Today';

// The file an asset id names, as the url the editor view gave it; null when the manifest names a
// file the view does not list (it may be mid-upload), or names none.
function urlOf(assets: readonly TemplateAsset[], id: string | undefined): string | null {
  if (id === undefined) return null;
  return assets.find((asset) => asset.assetId === id)?.url ?? null;
}

// The look the draft's slides are drawn in: what the server's look would send for a deck on this
// template, built from the manifest as it is on screen, so a changed field redraws at once. An
// asset id becomes the url of the matching `assets` entry; a manifest with no palette of its own
// draws in `basePalette`. The shadow is the card lift on the look's paper (cardLift), as on the
// server. Pure: no request, no clock.
export function lookFromManifest(
  manifest: TemplateManifest,
  assets: readonly TemplateAsset[],
  basePalette: LookPalette,
): LookView {
  const own = manifest.palette;
  const { extras: ownExtras = [], ...ownTokens } = own ?? {};
  const tokens: PaletteTokens = { ...basePalette.tokens, ...(own ? ownTokens : {}) };
  const extras: LookExtra[] = ownExtras.map(({ name, colour, ink }) => ({ name, colour, ink }));
  for (const { name, colour, ink } of extras) {
    (tokens as Record<string, string>)[name] = colour;
    (tokens as Record<string, string>)[`${name}-ink`] = ink;
  }
  const { chrome } = manifest;
  const art: LookArt =
    manifest.titleArt.kind === 'generator'
      ? { kind: 'generator', generator: manifest.titleArt.generator ?? null, url: null }
      : manifest.titleArt.kind === 'image'
        ? { kind: 'image', generator: null, url: urlOf(assets, manifest.titleArt.file) }
        : { kind: 'none', generator: null, url: null };
  const logos: LookLogos = {
    light: urlOf(assets, manifest.logos.light),
    dark: urlOf(assets, manifest.logos.dark),
    showOnDark: manifest.logos.showOnDark,
  };
  return {
    template: {
      name: manifest.name,
      label: manifest.label,
      layouts: manifest.layouts as LayoutName[],
      chrome: {
        tracker: chrome.tracker,
        footer: chrome.footer,
        progress: chrome.progress,
        deepDivesOnDivider: chrome.deepDivesOnDivider,
        draft: chrome.draft,
      },
      type: manifest.type,
      shape: { ...manifest.shape, shadow: cardLift(manifest.shape.shadow, tokens.paper) },
      ornament: 'dots',
      titleArt: null,
    },
    palette: own
      ? { name: 'template', label: 'Template', tokens }
      : { name: basePalette.name, label: basePalette.label, tokens },
    logo: null,
    fonts: manifest.fonts.flatMap((font): LookFont[] => {
      const url = urlOf(assets, font.file);
      return url === null
        ? []
        : [{ family: font.family, role: font.role, url, weight: font.weight, style: font.style }];
    }),
    logos,
    art,
    ornament: { colours: manifest.ornament.dots as LookView['ornament']['colours'] },
    topBar: chrome.topBar,
    templateRef: manifest.name,
    templateVersion: null,
    latestVersion: null,
    extras,
    customLayouts: [],
    templateMissing: false,
    draft: chrome.draft,
    footerDateLabel: SAMPLE_DATE,
    joinBar: false,
  };
}

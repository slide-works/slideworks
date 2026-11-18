import type { LookView, LooksPalette, LooksTemplate } from '../api/types';

// A preview look from one GET /api/looks template and palette (SW-2 L10), for
// the New deck dialog and the Look panel: no logo, the template's own chrome
// and draft setting, the footer date label the caller has from the server, and the join bar off.
// The catalogue row carries none of a template's files, so the preview has what a
// built-in has: no fonts, logos, ornament colours, top bar or extras, and the
// title art its titleArt names.
export function lookOf(
  template: Pick<
    LooksTemplate,
    | 'name'
    | 'label'
    | 'layouts'
    | 'chrome'
    | 'type'
    | 'shape'
    | 'ornament'
    | 'titleArt'
    | 'ref'
    | 'version'
  >,
  palette: Pick<LooksPalette, 'name' | 'label' | 'tokens'>,
  footerDateLabel: string,
): LookView {
  return {
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
    art:
      template.titleArt === null
        ? { kind: 'none', generator: null, url: null }
        : { kind: 'generator', generator: template.titleArt, url: null },
    ornament: { colours: [] },
    topBar: 'none',
    templateRef: template.ref,
    templateVersion: template.version,
    latestVersion: template.version,
    extras: [],
    customLayouts: [],
    templateMissing: false,
    draft: template.chrome.draft,
    footerDateLabel,
    joinBar: false,
  };
}

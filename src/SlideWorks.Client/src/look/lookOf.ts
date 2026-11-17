import type { LookView, LooksPalette, LooksTemplate } from '../api/types';

// A preview look from one GET /api/looks template and palette (SW-2 L10), for
// the New deck dialog and the Look panel: no logo, the template's own chrome
// and draft setting, and the footer date label the caller has from the server.
export function lookOf(
  template: Pick<
    LooksTemplate,
    'name' | 'label' | 'layouts' | 'chrome' | 'type' | 'shape' | 'ornament' | 'titleArt'
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
    draft: template.chrome.draft,
    footerDateLabel,
  };
}

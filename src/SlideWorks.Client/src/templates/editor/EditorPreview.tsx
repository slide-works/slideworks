import palettesJson from '@contracts/palettes.json' with { type: 'json' };
import sampleDeckJson from '@contracts/sample-deck.json' with { type: 'json' };
import { useMemo } from 'react';
import type { DeckPlayView, LookPalette } from '../../api/types';
import { LookPreview } from '../../look/LookPreview';
import { TEMPLATES_TEST_IDS } from '../testIds';
import type { TemplateEditorView } from '../types';
import { lookFromManifest } from './preview/lookFromManifest';

const sampleDeck = sampleDeckJson as DeckPlayView;

// A title, a light content slide and a dark divider, in the order a deck shows them.
const PREVIEW_NUMBERS = [1, 3, 2];
export const PREVIEW_SLIDES = PREVIEW_NUMBERS.map((number) => {
  const slide = sampleDeck.slides.find((candidate) => candidate.number === number);
  if (!slide) throw new Error(`No slide ${number} in sample-deck.json`);
  return slide;
});

// The palette a template that brings none draws in: the catalogue's default.
export const BASE_PALETTE: LookPalette = (() => {
  const palette = palettesJson.palettes.find((candidate) => candidate.isDefault);
  if (!palette) throw new Error('No default palette in palettes.json');
  return {
    name: palette.name,
    label: palette.label,
    tokens: palette.tokens as LookPalette['tokens'],
  };
})();

// The editor's live preview: three sample slides drawn by the real slide renderer in the look the
// draft's manifest describes. `draft` is what is on screen, so a field that was just changed is
// already in it.
export function EditorPreview({ draft }: { draft: TemplateEditorView }) {
  const look = useMemo(
    () => lookFromManifest(draft.manifest, draft.assets, BASE_PALETTE),
    [draft.manifest, draft.assets],
  );
  return (
    <section
      data-testid={TEMPLATES_TEST_IDS.preview}
      aria-labelledby="template-editor-preview-heading"
      className="grid gap-2"
    >
      <h2 id="template-editor-preview-heading" className="text-sm font-semibold text-ink">
        Preview
      </h2>
      <p className="sr-only">Preview of three sample slides in this template</p>
      <LookPreview look={look} slides={PREVIEW_SLIDES} />
    </section>
  );
}

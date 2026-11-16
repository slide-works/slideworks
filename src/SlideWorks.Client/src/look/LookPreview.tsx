import type { LookView } from '../api/types';
import type { RenderableSlide } from '../slides/SlideRenderer';

export type LookPreviewProps = { look: LookView; slides: RenderableSlide[] };

// TODO: a shell; the live preview of a few slides under the chosen look, for
// the New deck dialog and the Look panel, replaces it.
export function LookPreview({ look }: LookPreviewProps) {
  return (
    <div
      data-testid="look-preview"
      data-template={look.template.name}
      data-palette={look.palette.name}
    />
  );
}

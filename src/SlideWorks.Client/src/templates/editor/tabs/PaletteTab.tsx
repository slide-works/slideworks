import { editorTabTestId } from '../../testIds';
import type { EditorTabProps } from '../tabProps';

// TODO: a placeholder. The Palette tab of the template editor replaces it.
export function PaletteTab({ draft }: EditorTabProps) {
  return (
    <section data-testid={editorTabTestId('palette')} aria-label={`Palette of ${draft.name}`}>
      <h2 className="text-sm font-semibold text-ink">Palette</h2>
    </section>
  );
}

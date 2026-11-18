import { editorTabTestId } from '../../testIds';
import type { EditorTabProps } from '../tabProps';

// TODO: a placeholder. The Fonts tab of the template editor replaces it.
export function FontsTab({ draft }: EditorTabProps) {
  return (
    <section data-testid={editorTabTestId('fonts')} aria-label={`Fonts of ${draft.name}`}>
      <h2 className="text-sm font-semibold text-ink">Fonts</h2>
    </section>
  );
}

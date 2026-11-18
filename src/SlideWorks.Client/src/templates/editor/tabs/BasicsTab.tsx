import { editorTabTestId } from '../../testIds';
import type { EditorTabProps } from '../tabProps';

// TODO: a placeholder. The Basics tab of the template editor replaces it.
export function BasicsTab({ draft }: EditorTabProps) {
  return (
    <section data-testid={editorTabTestId('basics')} aria-label={`Basics of ${draft.name}`}>
      <h2 className="text-sm font-semibold text-ink">Basics</h2>
    </section>
  );
}

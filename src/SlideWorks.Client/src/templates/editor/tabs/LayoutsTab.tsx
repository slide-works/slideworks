import { editorTabTestId } from '../../testIds';
import type { EditorTabProps } from '../tabProps';

// TODO: a placeholder. The Custom layouts tab of the template editor replaces it.
export function LayoutsTab({ draft }: EditorTabProps) {
  return (
    <section data-testid={editorTabTestId('layouts')} aria-label={`Custom layouts of ${draft.name}`}>
      <h2 className="text-sm font-semibold text-ink">Custom layouts</h2>
    </section>
  );
}

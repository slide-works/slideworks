import { TEMPLATES_TEST_IDS } from '../testIds';
import type { TemplateEditorView } from '../types';

// TODO: a placeholder. The editor's live preview of the draft's slides replaces it.
export function EditorPreview({ draft }: { draft: TemplateEditorView }) {
  return (
    <section data-testid={TEMPLATES_TEST_IDS.preview} aria-label={`Preview of ${draft.name}`}>
      <h2 className="text-sm font-semibold text-ink">Preview</h2>
    </section>
  );
}

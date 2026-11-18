import { TEMPLATES_TEST_IDS } from '../testIds';
import type { EditorTabProps } from './tabProps';

// TODO: a placeholder. The publish checklist (its rows, Publish and "Update my decks") replaces it.
export function PublishChecklist({ draft }: EditorTabProps) {
  return (
    <section
      data-testid={TEMPLATES_TEST_IDS.checklist}
      aria-label={`Publish checklist of ${draft.name}`}
    >
      <h2 className="text-sm font-semibold text-ink">Publish checklist</h2>
    </section>
  );
}

import { useId } from 'react';
import { TEMPLATES_TEST_IDS } from './testIds';

export type NewTemplateDialogProps = { onClose(): void };

// TODO: a placeholder. The New template dialog (pick what to start from, name it, create the
// draft) replaces it. Rendered only while open.
// eslint-disable-next-line @typescript-eslint/no-unused-vars -- the props are the dialog's surface; the placeholder reads none.
export function NewTemplateDialog(_props: NewTemplateDialogProps) {
  const title = useId();
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={title}
      data-testid={TEMPLATES_TEST_IDS.newDialog}
    >
      <h2 id={title} className="text-sm font-semibold text-ink">
        New template
      </h2>
    </div>
  );
}

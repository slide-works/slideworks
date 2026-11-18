import { useId } from 'react';
import { TEMPLATES_TEST_IDS } from './testIds';

// Frozen: the templates page and the editor both open this dialog. It is rendered only while
// open, and whoever opens it puts focus back on the opener in onClose, as SharingDialog's does.
export type TemplateShareDialogProps = { templateId: string; name: string; onClose(): void };

// TODO: a placeholder. The share dialog (private, people you name, anyone with the link) replaces it.
export function TemplateShareDialog({ name }: TemplateShareDialogProps) {
  const title = useId();
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={title}
      data-testid={TEMPLATES_TEST_IDS.shareDialog}
    >
      <h2 id={title} className="text-sm font-semibold text-ink">
        Share {name}
      </h2>
    </div>
  );
}

import { useState } from 'react';
import { TemplateShareDialog } from '../TemplateShareDialog';

// Component tests only: a Share button that opens the template share dialog the way the templates
// page and the editor do, and puts focus back on itself when the dialog closes.
export function TemplateShareDialogHarness({
  templateId,
  name,
}: {
  templateId: string;
  name: string;
}) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" data-testid="open-template-share" onClick={() => setOpen(true)}>
        Share
      </button>
      {open && (
        <TemplateShareDialog templateId={templateId} name={name} onClose={() => setOpen(false)} />
      )}
    </>
  );
}

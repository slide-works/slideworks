import { useId, useState } from 'react';
import { ModalDialog } from '../../components/sharing/ModalDialog';
import { templatesApi } from '../api';
import type { TemplateRow } from '../types';
import { refusalText } from './refusalText';

// "Archive <name>?" before it happens. The dialog stays open on a failure and says why. Rendered
// only while open; ModalDialog puts focus back on the Archive button that opened it when it goes.
// onArchived runs once the server has archived the template, and the caller closes the dialog.
export function ArchiveDialog({
  row,
  onCancel,
  onArchived,
}: {
  row: TemplateRow;
  onCancel(): void;
  onArchived(): Promise<void>;
}) {
  const titleId = useId();
  const hintId = useId();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const archive = async () => {
    setBusy(true);
    setError(null);
    try {
      await templatesApi.archive(row.id);
    } catch (failure) {
      setBusy(false);
      setError(refusalText(failure) ?? `Could not archive ${row.name}. Try again.`);
      return;
    }
    await onArchived();
  };

  const keep =
    row.deckCount === 1 ? '1 deck keeps this look.' : `${row.deckCount} decks keep this look.`;

  return (
    <ModalDialog
      labelledBy={titleId}
      describedBy={hintId}
      testId="archive-template-dialog"
      widthClassName="max-w-md"
      onClose={() => !busy && onCancel()}
    >
      <div className="grid gap-4 p-5">
        <h2 id={titleId} className="text-base font-bold text-ink">
          Archive &quot;{row.name}&quot;?
        </h2>
        <p id={hintId} className="text-meta text-ink-muted">
          {keep} Nobody can pick it for a new deck.
        </p>
        {error !== null && (
          <p role="alert" className="text-meta text-bad">
            {error}
          </p>
        )}
        <div className="flex justify-end gap-2">
          <button type="button" className="btn-ghost" disabled={busy} onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="btn-primary" disabled={busy} onClick={archive}>
            Archive
          </button>
        </div>
      </div>
    </ModalDialog>
  );
}

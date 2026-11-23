import { useEffect, useId, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { SharingView } from '../api/types';
import { ModalDialog } from './sharing/ModalDialog';
import { SharingForm } from './sharing/SharingForm';

export type SharingDialogProps = {
  deckId: string;
  open: boolean;
  onClose(): void;
  onSaved(view: SharingView): void;
};

type Load =
  { kind: 'loading' } | { kind: 'loaded'; view: SharingView } | { kind: 'failed'; message: string };

// Who can open and present a deck (PNG 04): private, anyone with the link, or
// only the people listed. It reads GET /api/decks/{id}/sharing each time it opens, and
// Save PUTs the change, hands the saved view to onSaved and closes.
export function SharingDialog({ deckId, open, onClose, onSaved }: SharingDialogProps) {
  if (!open) return null;
  return <OpenSharingDialog deckId={deckId} onClose={onClose} onSaved={onSaved} />;
}

function OpenSharingDialog({ deckId, onClose, onSaved }: Omit<SharingDialogProps, 'open'>) {
  const titleId = useId();
  const descriptionId = useId();
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    api.get<SharingView>(`/api/decks/${encodeURIComponent(deckId)}/sharing`).then(
      (view) => active && setLoad({ kind: 'loaded', view }),
      (error: unknown) =>
        active &&
        setLoad({
          kind: 'failed',
          message: error instanceof ApiError ? error.message : "Can't reach SlideWorks.",
        }),
    );
    return () => {
      active = false;
    };
  }, [deckId]);

  return (
    <ModalDialog
      labelledBy={titleId}
      describedBy={descriptionId}
      testId="sharing-dialog"
      onClose={onClose}
    >
      <header className="px-[18px] pt-4 pb-1.5">
        <h2 id={titleId} className="text-base font-semibold text-ink">
          Share this deck
        </h2>
        <p id={descriptionId} className="mt-1 text-meta text-ink-muted">
          Who can open it, and who can edit it with you.
        </p>
      </header>
      {load.kind === 'loaded' ? (
        <SharingForm
          deckId={deckId}
          view={load.view}
          onCancel={onClose}
          onSaved={(view) => {
            onSaved(view);
            onClose();
          }}
        />
      ) : (
        <div className="grid gap-3 px-[18px] pt-2.5 pb-4">
          {load.kind === 'failed' ? (
            <p role="alert" data-testid="sharing-load-error" className="text-meta text-bad">
              {load.message}
            </p>
          ) : (
            <p role="status" className="text-meta text-ink-muted">
              Loading…
            </p>
          )}
          <button
            type="button"
            data-testid="sharing-cancel"
            className="btn-ghost justify-self-end"
            onClick={onClose}
          >
            Cancel
          </button>
        </div>
      )}
    </ModalDialog>
  );
}

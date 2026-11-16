import type { SharingView } from '../api/types';

export type SharingDialogProps = {
  deckId: string;
  open: boolean;
  onClose(): void;
  onSaved(view: SharingView): void;
};

// Who can open and present a deck (GET and PUT /api/decks/{id}/sharing).
// TODO: shell; the sharing dialog replaces it.
export function SharingDialog({ deckId, open, onClose }: SharingDialogProps) {
  if (!open) return null;
  return (
    <div role="dialog" aria-label="Share" data-testid="sharing-dialog" data-deck-id={deckId}>
      <button type="button" className="btn-ghost" onClick={onClose}>
        Close
      </button>
    </div>
  );
}

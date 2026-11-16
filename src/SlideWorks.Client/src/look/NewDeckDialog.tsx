import type { CreateDeckResult } from '../api/types';

export type NewDeckDialogProps = {
  open: boolean;
  onClose(): void;
  onCreated(result: CreateDeckResult): void;
};

// TODO: a shell with inert fields; the New deck dialog (title, template and
// palette from GET /api/looks, a live LookPreview, POST /api/decks) replaces it.
export function NewDeckDialog({ open }: NewDeckDialogProps) {
  if (!open) return null;
  return (
    <section data-testid="new-deck-dialog" aria-label="New deck" className="panel grid gap-3 p-4">
      <h2 className="text-base font-semibold text-ink">New deck</h2>
      <label className="grid gap-1 text-meta">
        Title
        <input data-testid="new-deck-title" type="text" disabled />
      </label>
      <label className="grid gap-1 text-meta">
        Template
        <select data-testid="new-deck-template" disabled />
      </label>
      <label className="grid gap-1 text-meta">
        Palette
        <select data-testid="new-deck-palette" disabled />
      </label>
      <button data-testid="new-deck-create" type="button" disabled>
        Create deck
      </button>
    </section>
  );
}

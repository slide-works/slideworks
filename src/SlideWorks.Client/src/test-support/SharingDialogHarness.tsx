import { useState } from 'react';
import { SharingDialog } from '../components/SharingDialog';

// Component tests only: a Share button that opens the sharing dialog the way the
// deck page does, and shows the level onSaved last reported ("none" before).
export function SharingDialogHarness({ deckId }: { deckId: string }) {
  const [open, setOpen] = useState(false);
  const [saved, setSaved] = useState('none');
  return (
    <>
      <button type="button" data-testid="open-sharing" onClick={() => setOpen(true)}>
        Share
      </button>
      <output data-testid="saved-level">{saved}</output>
      <SharingDialog
        deckId={deckId}
        open={open}
        onClose={() => setOpen(false)}
        onSaved={(view) => setSaved(view.level)}
      />
    </>
  );
}

import { useTemplateDraft } from '../useTemplateDraft';

// Component tests only: useTemplateDraft on its own, showing what it holds, with buttons that
// change the draft once, twice in a row, and read it again.
export function TemplateDraftHarness({ id }: { id: string }) {
  const { load, saving, saveError, change, reload } = useTemplateDraft(id);
  return (
    <div>
      <output data-testid="draft-state">{load.kind}</output>
      <output data-testid="draft-revision">{load.kind === 'loaded' && load.draft.revision}</output>
      <output data-testid="draft-purpose">
        {load.kind === 'loaded' && load.draft.manifest.purpose}
      </output>
      <output data-testid="draft-load-error">{load.kind === 'failed' && load.message}</output>
      <output data-testid="draft-saving">{String(saving)}</output>
      <output data-testid="draft-save-code">{saveError?.code}</output>
      <output data-testid="draft-save-message">{saveError?.message}</output>
      <button type="button" onClick={() => void change({ purpose: 'A new purpose.' })}>
        Change purpose
      </button>
      <button
        type="button"
        onClick={() => {
          void change({ purpose: 'A new purpose.' });
          void change({ lockPalette: false });
        }}
      >
        Change twice
      </button>
      <button type="button" onClick={() => void reload()}>
        Reload
      </button>
    </div>
  );
}

import { useState } from 'react';
import type { TemplateManifest } from '../types';
import { useTemplateDraft, type ChangeOutcome } from '../useTemplateDraft';

// Component tests only: useTemplateDraft on its own, showing what it holds, with buttons that
// change the draft once, twice in a row, and read it again. `draft-outcome` is what the last
// change resolved to, as JSON.
export function TemplateDraftHarness({ id }: { id: string }) {
  const { load, saving, saveError, change: save, reload } = useTemplateDraft(id);
  const [outcome, setOutcome] = useState<ChangeOutcome | null>(null);
  const change = (patch: Partial<TemplateManifest>) => save(patch).then(setOutcome);
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
      <output data-testid="draft-outcome">{outcome && JSON.stringify(outcome)}</output>
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

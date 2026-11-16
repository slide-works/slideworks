import { useState } from 'react';
import { ApiError, api } from '../api/client';
import type { DeckPageLook, DeckPageView, LookChangeRequest, LookChangeResult } from '../api/types';
import { ChromeRow } from './panel/ChromeRow';
import { deepDiveNumbers } from './panel/deepDiveNumbers';
import { LogoField } from './panel/LogoField';
import { PaletteRow } from './panel/PaletteRow';
import { ShareCardPreview } from './panel/ShareCardPreview';
import { useLooks } from './panel/useLooks';

export type LookPanelProps = { deck: DeckPageView; onSaved(look: DeckPageLook): void };

type Save =
  | { kind: 'idle' }
  | { kind: 'saving' }
  | { kind: 'saved'; warnings: string[] }
  | { kind: 'failed'; message: string };

// The deck's look (mockup section 08): template, palette with its swatch and
// contrast check, chrome, logo, draft, the deep-dive slides and the share card.
// Every change is one PUT /api/decks/{id}/look; the page re-reads on DeckChanged.
export function LookPanel({ deck, onSaved }: LookPanelProps) {
  const { look } = deck;
  const looksLoad = useLooks();
  const looks = looksLoad.kind === 'loaded' ? looksLoad.looks : null;
  const [save, setSave] = useState<Save>({ kind: 'idle' });
  const saving = save.kind === 'saving';
  const template = looks?.templates.find((entry) => entry.name === look.template.name);
  const lockedBy = template?.lockedPalette ? template.label : null;
  const deepDives = deepDiveNumbers(deck.slides);

  const change = (body: LookChangeRequest) => {
    setSave({ kind: 'saving' });
    api.put<LookChangeResult>(`/api/decks/${encodeURIComponent(deck.id)}/look`, body).then(
      (result) => {
        setSave({ kind: 'saved', warnings: result.warnings });
        onSaved(result.look);
      },
      (error: unknown) =>
        setSave({
          kind: 'failed',
          message: error instanceof ApiError ? error.message : "Can't reach SlideWorks.",
        }),
    );
  };

  return (
    <section data-testid="look-panel" aria-label="Look" className="panel grid gap-3 p-4">
      <h2 className="text-base font-semibold text-ink">Look</h2>
      {looksLoad.kind === 'failed' && (
        <p role="alert" data-testid="look-looks-error" className="text-meta text-bad">
          {looksLoad.message}
        </p>
      )}
      <label className="grid gap-1 text-meta">
        <span className="font-semibold text-ink-muted">Template</span>
        <select
          data-testid="look-template"
          className="field"
          value={look.template.name}
          disabled={saving || looks === null}
          onChange={(event) => change({ template: event.target.value })}
        >
          {looks ? (
            looks.templates.map((entry) => (
              <option key={entry.name} value={entry.name}>
                {entry.label}
              </option>
            ))
          ) : (
            <option value={look.template.name}>{look.template.label}</option>
          )}
        </select>
        {template && <span className="text-ink-muted">{template.purpose}</span>}
      </label>
      <PaletteRow
        look={look}
        looks={looks}
        lockedBy={lockedBy}
        disabled={saving}
        onChange={(palette) => change({ palette })}
      />
      <ChromeRow
        chrome={look.template.chrome}
        disabled={saving}
        onChange={(chrome) => change({ chrome })}
      />
      <LogoField deckId={deck.id} logo={look.logo} onChange={(logo) => change({ logo })} />
      <label className="text-meta">
        <input
          data-testid="look-draft"
          type="checkbox"
          checked={look.draft}
          disabled={saving}
          onChange={(event) => change({ draft: event.target.checked })}
        />{' '}
        Draft: stamp the title and every footer
      </label>
      <div className="grid gap-1 text-meta">
        <span className="font-semibold text-ink-muted">Deep dives</span>
        <span data-testid="look-deep-dives">{deepDives ?? 'None'}</span>
        <span className="text-ink-muted">Set per slide.</span>
      </div>
      {save.kind === 'failed' && (
        <p role="alert" data-testid="look-error" className="text-meta text-bad">
          {save.message}
        </p>
      )}
      {save.kind === 'saved' && save.warnings.length > 0 && (
        <ul data-testid="look-warnings" role="status" className="grid gap-1 text-meta text-warn">
          {save.warnings.map((warning) => (
            <li key={warning} data-testid="look-warning">
              {warning}
            </li>
          ))}
        </ul>
      )}
      <ShareCardPreview deck={deck} />
    </section>
  );
}

import { useEffect, useRef, useState } from 'react';
import { ApiError, api } from '../api/client';
import type { DeckPageLook, DeckPageView, LookChangeRequest, LookChangeResult } from '../api/types';
import { ChromeRow } from './panel/ChromeRow';
import { deepDiveNumbers } from './panel/deepDiveNumbers';
import { LogoField } from './panel/LogoField';
import { PaletteRow } from './panel/PaletteRow';
import { ShareCardPreview } from './panel/ShareCardPreview';
import { VersionOffer, VersionUpdated, type VersionUpdate } from './panel/VersionOffer';
import { useLooks } from './useLooks';

const UPDATE_FAILED = 'Could not update this deck. Try again.';

// The server refuses a template the caller may not pick as "Unknown template '<id>'" (LookRules.UnknownTemplate) and
// sends no code with it. The update only ever targets the deck's own template, so this is a share that was taken back.
const UNKNOWN_TEMPLATE = /^Unknown template\b/;

// A refusal's own sentence (its message, or an error that is itself a sentence), never a code.
function updateFailure(error: unknown, version: number): string {
  if (!(error instanceof ApiError)) return UPDATE_FAILED;
  if (error.status === 400 && UNKNOWN_TEMPLATE.test(error.detail ?? error.code ?? '')) {
    return `This template isn't shared with you any more, so your deck stays on version ${version}.`;
  }
  if (error.detail) return error.detail;
  return error.code !== undefined && /\s/.test(error.code) ? error.code : UPDATE_FAILED;
}

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
  const [update, setUpdate] = useState<VersionUpdate>({ kind: 'idle' });
  const updateButton = useRef<HTMLButtonElement>(null);
  const updatedMessage = useRef<HTMLParagraphElement>(null);
  // What the deck stores: an uploaded template's tpl_ id, else the built-in's name.
  const templateRef = look.templateRef ?? look.template.name;
  const template = looks?.templates.find((entry) => entry.ref === templateRef);
  const offered =
    deck.isOwner &&
    update.kind !== 'done' &&
    look.templateVersion !== null &&
    look.latestVersion !== null &&
    look.latestVersion > look.templateVersion
      ? { version: look.templateVersion, latest: look.latestVersion }
      : null;

  useEffect(() => {
    if (update.kind === 'done') updatedMessage.current?.focus();
    if (update.kind === 'failed') updateButton.current?.focus();
  }, [update.kind]);
  const lockedBy = template?.lockedPalette ? template.label : null;
  const deepDives = deepDiveNumbers(deck.slides);

  const change = (body: LookChangeRequest) => {
    setSave({ kind: 'saving' });
    setUpdate({ kind: 'idle' });
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

  // Moves the deck to the template's latest published version.
  const updateToLatest = () => {
    if (!offered) return;
    setSave({ kind: 'saving' });
    setUpdate({ kind: 'running' });
    const body: LookChangeRequest = { templateVersion: 'latest' };
    api.put<LookChangeResult>(`/api/decks/${encodeURIComponent(deck.id)}/look`, body).then(
      (result) => {
        setSave({ kind: 'saved', warnings: result.warnings });
        setUpdate({ kind: 'done', version: result.look.templateVersion ?? offered.latest });
        onSaved(result.look);
      },
      (error: unknown) => {
        setSave({ kind: 'idle' });
        setUpdate({ kind: 'failed', message: updateFailure(error, offered.version) });
      },
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
          value={templateRef}
          disabled={saving || looks === null}
          onChange={(event) => change({ template: event.target.value })}
        >
          {looks ? (
            <>
              {template === undefined && <option value={templateRef}>{look.template.label}</option>}
              {looks.templates.map((entry) => (
                <option key={entry.ref} value={entry.ref}>
                  {entry.label}
                </option>
              ))}
            </>
          ) : (
            <option value={templateRef}>{look.template.label}</option>
          )}
        </select>
        {template && <span className="text-ink-muted">{template.purpose}</span>}
      </label>
      {offered && (
        <VersionOffer
          templateLabel={look.template.label}
          version={offered.version}
          latest={offered.latest}
          update={update}
          buttonRef={updateButton}
          onUpdate={updateToLatest}
        />
      )}
      {update.kind === 'done' && (
        <VersionUpdated version={update.version} messageRef={updatedMessage} />
      )}
      <PaletteRow
        look={look}
        looks={looks}
        lockedBy={lockedBy}
        disabled={saving}
        onChange={(palette) => change({ palette })}
      />
      <ChromeRow
        chrome={look.template.chrome}
        topBar={look.topBar}
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

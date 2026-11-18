import sampleDeckJson from '@contracts/sample-deck.json' with { type: 'json' };
import { useRef, useState, type FormEvent } from 'react';
import { ApiError, api } from '../api/client';
import type {
  CreateDeckRequest,
  CreateDeckResult,
  DeckPlayView,
  LooksPalette,
  LooksTemplate,
  LooksView,
} from '../api/types';
import { ModalDialog } from '../components/sharing/ModalDialog';
import { LookPreview } from './LookPreview';
import { lookOf } from './lookOf';
import { useLooks } from './useLooks';

export type NewDeckDialogProps = {
  open: boolean;
  onClose(): void;
  onCreated(result: CreateDeckResult): void;
  // The ref of a template to start on (the address /decks?template=<ref>). One the person may not pick opens the dialog on the default.
  initialTemplate?: string | null;
};

// The sample deck's title slide and one content slide, as in the mockup.
const sampleDeck = sampleDeckJson as DeckPlayView;
const PREVIEW_SLIDES = sampleDeck.slides.filter((slide) => [1, 8].includes(slide.number));

// GET /api/looks carries no date, so the preview's footer has none (no Date in the SPA).
const NO_FOOTER_DATE = '';

type Choice = { template: LooksTemplate; palette: LooksPalette };

// The palette a template starts with: the one it locks, else the catalogue's default.
// A template's own palette is listed under this reserved name, and GET /api/looks carries no tokens for it, so
// its preview is drawn with the default palette's (TODO: draw it in the template's own palette once the row carries it).
function paletteFor(looks: LooksView, template: LooksTemplate): LooksPalette | undefined {
  const fallback = looks.palettes.find((palette) => palette.isDefault);
  if (template.lockedPalette === null) return fallback;
  return looks.palettes.find((palette) => palette.name === template.lockedPalette) ?? fallback;
}

function choiceOf(looks: LooksView, template: LooksTemplate | undefined): Choice | null {
  const palette = template && paletteFor(looks, template);
  return template && palette ? { template, palette } : null;
}

function defaultChoice(looks: LooksView): Choice | null {
  return choiceOf(
    looks,
    looks.templates.find((entry) => entry.isDefault),
  );
}

// The palette name a deck on this choice is created with: the one the template sets, as the server lists it.
const paletteNameOf = (choice: Choice) => choice.template.lockedPalette ?? choice.palette.name;

// The palette select's label for a locked palette: the template's own palette is named after the template.
function lockedLabel(choice: Choice): string {
  const name =
    choice.template.lockedPalette === 'template' ? choice.template.label : choice.palette.label;
  return `${name} (set by the template)`;
}

// The caller's own templates first, then those shared with them, then the built-ins; each keeps the server's order.
function inGroups(templates: LooksTemplate[]): LooksTemplate[] {
  const own = templates.filter((entry) => !entry.builtIn && entry.owner === null);
  const shared = templates.filter((entry) => !entry.builtIn && entry.owner !== null);
  return [...own, ...shared, ...templates.filter((entry) => entry.builtIn)];
}

const errorText = (error: unknown) =>
  error instanceof ApiError ? error.message : "Can't reach SlideWorks.";

// "+ New deck", a modal <dialog> (the page behind is inert, Tab wraps, Esc closes): a title, a template and a palette from GET /api/looks (a static
// catalogue, SW-2 D20), with a live preview of two sample slides under the
// chosen look. A template that locks its palette greys the palette out. Create
// POSTs {title, template, palette} to /api/decks; a failed create shows the
// server's error and keeps the dialog open.
export function NewDeckDialog({ open, onClose, onCreated, initialTemplate }: NewDeckDialogProps) {
  if (!open) return null;
  return <OpenDialog onClose={onClose} onCreated={onCreated} initialTemplate={initialTemplate} />;
}

function OpenDialog({ onClose, onCreated, initialTemplate }: Omit<NewDeckDialogProps, 'open'>) {
  const catalogue = useLooks();
  // The person's own pick; until they make one, the catalogue's defaults.
  const [picked, setPicked] = useState<Choice | null>(null);
  const [title, setTitle] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const titleRef = useRef<HTMLInputElement>(null);

  const looks = catalogue.kind === 'loaded' ? catalogue.looks : null;
  const named = looks?.templates.find((entry) => entry.ref === initialTemplate);
  const unavailable = Boolean(initialTemplate) && looks !== null && named === undefined;
  const choice = picked ?? (looks ? (choiceOf(looks, named) ?? defaultChoice(looks)) : null);
  const locked = choice !== null && choice.template.lockedPalette !== null;

  const chooseTemplate = (template: LooksTemplate) => {
    if (!looks || !choice) return;
    const forced = template.lockedPalette === null ? undefined : paletteFor(looks, template);
    setPicked({ template, palette: forced ?? choice.palette });
  };

  const choosePalette = (name: string) => {
    const palette = looks?.palettes.find((entry) => entry.name === name);
    if (palette && choice) setPicked({ ...choice, palette });
  };

  const create = async (event: FormEvent) => {
    event.preventDefault();
    if (!choice) return;
    setSaving(true);
    setError(null);
    try {
      const request: CreateDeckRequest = {
        title,
        template: choice.template.ref,
        palette: paletteNameOf(choice),
      };
      onCreated(await api.post<CreateDeckResult>('/api/decks', request));
    } catch (failure) {
      setSaving(false);
      setError(errorText(failure));
      titleRef.current?.focus();
    }
  };

  return (
    <ModalDialog
      labelledBy="new-deck-heading"
      describedBy="new-deck-hint"
      testId="new-deck-dialog"
      widthClassName="max-w-4xl"
      onClose={onClose}
    >
      <form className="grid max-h-[calc(100dvh-3rem)] gap-0 overflow-y-auto" onSubmit={create}>
        <header className="flex flex-wrap items-baseline gap-3 border-b border-edge px-5 py-3">
          <h2 id="new-deck-heading" className="text-base font-bold text-ink">
            New deck
          </h2>
          <span id="new-deck-hint" className="text-meta text-ink-muted">
            An agent makes the same choices with <code>create_deck {'{template, palette}'}</code>.
          </span>
        </header>

        <div data-testid="new-deck-form" className="grid gap-5 p-5 md:grid-cols-2">
          <div className="grid content-start gap-4">
            <label className="grid gap-1">
              <span className="text-xs font-semibold tracking-wider text-ink-muted uppercase">
                Title
              </span>
              <input
                ref={titleRef}
                autoFocus
                required
                aria-invalid={error !== null}
                aria-describedby={error ? 'new-deck-error' : undefined}
                data-testid="new-deck-title"
                className="field"
                value={title}
                onChange={(event) => setTitle(event.target.value)}
              />
            </label>

            {catalogue.kind === 'loading' && (
              <p
                role="status"
                data-testid="new-deck-looks-loading"
                className="text-meta text-ink-muted"
              >
                Loading templates and palettes…
              </p>
            )}

            {catalogue.kind === 'failed' && (
              <p role="alert" data-testid="new-deck-looks-error" className="text-meta text-bad">
                {catalogue.message}
              </p>
            )}

            {looks && choice && (
              <>
                <fieldset className="grid gap-1.5">
                  <legend className="mb-1 text-xs font-semibold tracking-wider text-ink-muted uppercase">
                    Template
                  </legend>
                  {unavailable && (
                    <p
                      role="status"
                      data-testid="new-deck-template-unavailable"
                      className="text-meta text-ink-muted"
                    >
                      That template is not available to you.
                    </p>
                  )}
                  {inGroups(looks.templates).map((template) => (
                    <label
                      key={template.ref}
                      data-testid="new-deck-template"
                      data-template={template.ref}
                      className={`grid cursor-pointer grid-cols-[16px_minmax(0,1fr)] gap-2.5 rounded-md border px-3 py-2 ${
                        template.ref === choice.template.ref
                          ? 'border-accent bg-accent-soft'
                          : 'border-edge'
                      }`}
                    >
                      <input
                        type="radio"
                        data-testid={`new-deck-template-${template.ref}`}
                        name="template"
                        value={template.ref}
                        checked={template.ref === choice.template.ref}
                        onChange={() => chooseTemplate(template)}
                        className="mt-1 accent-accent"
                      />
                      <span>
                        <span className="flex flex-wrap items-center gap-x-2">
                          <b className="font-semibold text-ink">{template.label}</b>
                          {!template.builtIn && (
                            <span className="rounded-full bg-accent-soft px-2 py-0.5 text-xs font-semibold text-accent-dark">
                              {template.owner === null ? 'yours' : `shared by ${template.owner}`}
                            </span>
                          )}
                        </span>
                        <span className="block text-meta text-ink-muted">{template.purpose}</span>
                      </span>
                    </label>
                  ))}
                </fieldset>

                <label className="grid gap-1.5">
                  <span className="text-xs font-semibold tracking-wider text-ink-muted uppercase">
                    Palette
                  </span>
                  <select
                    data-testid="new-deck-palette"
                    className="field"
                    value={choice.palette.name}
                    disabled={locked}
                    aria-describedby="new-deck-palette-note"
                    onChange={(event) => choosePalette(event.target.value)}
                  >
                    {locked ? (
                      <option value={paletteNameOf(choice)}>{lockedLabel(choice)}</option>
                    ) : (
                      looks.palettes.map((palette) => (
                        <option key={palette.name} value={palette.name}>
                          {palette.isDefault ? `${palette.label} (default)` : palette.label}
                        </option>
                      ))
                    )}
                  </select>
                  <span aria-hidden className="flex h-4 overflow-hidden rounded">
                    {choice.palette.swatch.map((colour, index) => (
                      <span key={index} className="flex-1" style={{ background: colour }} />
                    ))}
                  </span>
                  <span
                    id="new-deck-palette-note"
                    data-testid="new-deck-palette-note"
                    className="text-meta text-ink-muted"
                  >
                    {locked
                      ? `The ${choice.template.label} template locks its palette.`
                      : choice.palette.contrast.pass
                        ? 'All pairs pass.'
                        : `Lowest pair: ${choice.palette.contrast.lowest.pair}.`}
                  </span>
                </label>
              </>
            )}
          </div>

          {choice && (
            <div className="grid content-start gap-2">
              <LookPreview
                look={lookOf(choice.template, choice.palette, NO_FOOTER_DATE)}
                slides={PREVIEW_SLIDES}
              />
              <p data-testid="new-deck-preview-note" className="text-meta text-ink-muted">
                {choice.template.label} · {choice.palette.label}
              </p>
            </div>
          )}
        </div>

        <footer className="flex flex-wrap items-center gap-2 border-t border-edge px-5 py-3">
          {error && (
            <p
              id="new-deck-error"
              role="alert"
              data-testid="new-deck-error"
              className="mr-auto text-meta text-bad"
            >
              {error}
            </p>
          )}
          <span className="ml-auto" />
          <button
            type="button"
            data-testid="new-deck-cancel"
            className="btn-ghost"
            onClick={onClose}
          >
            Cancel
          </button>
          <button
            type="submit"
            data-testid="new-deck-create"
            className="btn-primary py-1.5"
            disabled={saving || choice === null}
          >
            Create deck
          </button>
        </footer>
      </form>
    </ModalDialog>
  );
}

import { useEffect, useId, useRef, useState, type ChangeEvent } from 'react';
import { editorTabTestId } from '../../testIds';
import type { TemplateFontRole, TemplateManifestFont } from '../../types';
import type { ChangeOutcome } from '../../useTemplateDraft';
import { templateErrorMessage } from '../../api';
import { omit } from '../basics/fields';
import { FontFileRow } from '../fonts/FontFileRow';
import { FontRightsTick } from '../fonts/FontRightsTick';
import { FontToAddRow } from '../fonts/FontToAddRow';
import { RefusedFileRow, type FileProblem } from '../fonts/RefusedFileRow';
import type { EditorTabProps } from '../tabProps';
import { confirmFontRights, uploadAsset } from '../uploadAsset';

type FamilyField = 'head' | 'body';
const FAMILIES: { field: FamilyField; label: string }[] = [
  { field: 'head', label: 'Heading family' },
  { field: 'body', label: 'Body family' },
];

const NOT_ALLOWED_YET = 'Tick the box above first: the right to serve fonts comes before any file.';

// The Fonts tab: the heading and body families, the font files the template uses, and uploading more.
// A file is uploaded only after the person has said they may serve it; it is then added to the
// manifest with a role, a family, a weight and a style, and its asset id as `file`. Removing takes a
// file out of the manifest and leaves the stored bytes. A failed save with no field of its own is
// shown by the editor's frame.
export function FontsTab({ draft, change, reload }: EditorTabProps) {
  const id = useId();
  const manifest = draft.manifest;
  const fontAssets = draft.assets.filter((asset) => asset.kind === 'font');
  const used = new Set(manifest.fonts.map((font) => font.file));
  const toAdd = fontAssets.filter((asset) => !used.has(asset.assetId));
  const [ticked, setTicked] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [tickError, setTickError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [uploading, setUploading] = useState('');
  const [problems, setProblems] = useState<FileProblem[]>([]);
  const [typed, setTyped] = useState<Partial<Record<FamilyField, string>>>({});
  const [familyErrors, setFamilyErrors] = useState<Partial<Record<FamilyField, string>>>({});
  // Where focus goes once the page has caught up: "Add font files", or the file after a removed one.
  const focusNext = useRef<'add' | { file: string; index: number } | null>(null);
  const picker = useRef<HTMLInputElement>(null);
  const addButton = useRef<HTMLButtonElement>(null);
  const section = useRef<HTMLElement>(null);
  const sent = useRef<Partial<Record<FamilyField, string>>>({});

  const rightsOn = ticked || (fontAssets.length > 0 && fontAssets.every((a) => a.rightsConfirmed));

  useEffect(() => {
    const next = focusNext.current;
    if (next === null || busy) return;
    if (next === 'add') {
      focusNext.current = null;
      addButton.current?.focus();
    } else if (!manifest.fonts.some((font) => font.file === next.file)) {
      focusNext.current = null;
      const buttons = section.current?.querySelectorAll<HTMLElement>('[data-font-remove]') ?? [];
      (buttons[Math.min(next.index, buttons.length - 1)] ?? addButton.current)?.focus();
    }
  }, [busy, manifest.fonts]);

  // The tick shows at once; if the server cannot confirm, it goes back off with the reason.
  async function confirmRights() {
    setConfirming(true);
    setTicked(true);
    setTickError(null);
    try {
      await confirmFontRights(draft.id);
      await reload();
    } catch (error) {
      setTicked(false);
      setTickError(templateErrorMessage(error));
    } finally {
      setConfirming(false);
    }
  }

  // One file after another: the server takes one upload per template at a time.
  async function addFiles(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    event.target.value = '';
    if (files.length === 0) return;
    setBusy(true);
    const refused: FileProblem[] = [];
    setProblems(refused);
    let stored = false;
    try {
      for (const file of files) {
        setUploading(`Uploading ${file.name}`);
        const outcome = await uploadAsset(draft.id, 'font', file, true);
        if (outcome.ok) {
          stored = true;
        } else {
          refused.push({
            key: refused.length,
            fileName: file.name,
            pill: outcome.kind === 'refused' ? 'refused' : 'not uploaded',
            message:
              outcome.kind === 'unreachable'
                ? `Could not upload ${file.name}. Try again.`
                : outcome.message,
          });
          setProblems([...refused]);
        }
      }
      if (stored) await reload();
    } finally {
      setUploading('');
      setBusy(false);
      focusNext.current = 'add';
    }
  }

  async function addFont(font: TemplateManifestFont): Promise<ChangeOutcome> {
    const outcome = await change({ fonts: [...manifest.fonts, font] });
    if (outcome.ok) focusNext.current = 'add';
    return outcome;
  }

  async function removeFont(index: number) {
    const font = manifest.fonts[index];
    const outcome = await change({ fonts: manifest.fonts.filter((_, at) => at !== index) });
    if (outcome.ok) focusNext.current = { file: font.file, index };
  }

  const familyOf = (role: TemplateFontRole) => (role === 'mono' ? '' : manifest.type[role]);

  async function commitFamily(field: FamilyField) {
    const value = typed[field];
    if (value === undefined) return;
    if (value === manifest.type[field]) {
      setTyped((current) => omit(current, field));
      return;
    }
    if (sent.current[field] === value) return;
    sent.current[field] = value;
    setFamilyErrors((current) => omit(current, field));
    const outcome = await change({ type: { ...manifest.type, [field]: value } });
    if (outcome.ok) {
      setTyped((current) => omit(current, field));
      delete sent.current[field];
      return;
    }
    // Typed text stays: it is the person's. A refusal shows beside the field and takes focus; a
    // failure with no field of its own (no answer, a stale revision) is the frame's.
    if (outcome.code === null || outcome.code === 'stale_revision') {
      delete sent.current[field];
      return;
    }
    setFamilyErrors((current) => ({ ...current, [field]: outcome.message }));
    document.getElementById(`${id}-${field}`)?.focus();
  }

  return (
    <section
      ref={section}
      data-testid={editorTabTestId('fonts')}
      aria-labelledby={`${id}-heading`}
      className="panel grid gap-4 p-4"
    >
      <h2 id={`${id}-heading`} className="text-sm font-semibold text-ink">
        Fonts
      </h2>

      <div className="grid gap-4 sm:grid-cols-2">
        {FAMILIES.map(({ field, label }) => (
          <div key={field} className="grid gap-1">
            <label htmlFor={`${id}-${field}`} className="text-meta font-semibold text-ink-muted">
              {label}
            </label>
            <input
              id={`${id}-${field}`}
              type="text"
              className="field"
              value={typed[field] ?? manifest.type[field]}
              aria-invalid={familyErrors[field] === undefined ? undefined : true}
              aria-describedby={
                familyErrors[field] === undefined ? undefined : `${id}-${field}-error`
              }
              onChange={(event) => {
                delete sent.current[field];
                setFamilyErrors((current) => omit(current, field));
                setTyped((current) => ({ ...current, [field]: event.target.value }));
              }}
              onBlur={() => void commitFamily(field)}
            />
            {familyErrors[field] === undefined ? null : (
              <p id={`${id}-${field}-error`} className="text-meta text-bad">
                {familyErrors[field]}
              </p>
            )}
          </div>
        ))}
      </div>

      <FontRightsTick
        checked={rightsOn}
        confirming={confirming}
        error={tickError}
        onConfirm={() => void confirmRights()}
      />

      <div className="grid gap-2">
        <h3 id={`${id}-files`} className="text-meta font-semibold text-ink-muted">
          Font files in this template
        </h3>
        {manifest.fonts.length === 0 ? (
          <p className="text-meta text-ink-muted">No font files yet.</p>
        ) : (
          <ul aria-labelledby={`${id}-files`} className="grid gap-2">
            {manifest.fonts.map((font, index) => (
              <FontFileRow
                key={`${font.file}-${index}`}
                font={font}
                asset={fontAssets.find((asset) => asset.assetId === font.file)}
                onRemove={() => void removeFont(index)}
              />
            ))}
          </ul>
        )}
      </div>

      {toAdd.length === 0 ? null : (
        <div className="grid gap-2">
          <h3 id={`${id}-unused`} className="text-meta font-semibold text-ink-muted">
            Uploaded, not added yet
          </h3>
          <ul aria-labelledby={`${id}-unused`} className="grid gap-2">
            {toAdd.map((asset) => (
              <FontToAddRow key={asset.assetId} asset={asset} familyOf={familyOf} onAdd={addFont} />
            ))}
          </ul>
        </div>
      )}

      <div className="grid gap-2">
        <div className="flex flex-wrap items-center gap-3">
          <button
            ref={addButton}
            type="button"
            className="btn-ghost"
            disabled={!rightsOn || busy}
            aria-describedby={rightsOn ? undefined : `${id}-why`}
            onClick={() => picker.current?.click()}
          >
            Add font files
          </button>
          <input
            ref={picker}
            type="file"
            multiple
            accept=".woff2,font/woff2"
            tabIndex={-1}
            aria-hidden="true"
            className="sr-only"
            onChange={(event) => void addFiles(event)}
          />
          {rightsOn ? null : (
            <p id={`${id}-why`} className="text-meta text-ink-muted">
              {NOT_ALLOWED_YET}
            </p>
          )}
          <p role="status" className="text-meta text-ink-muted">
            {uploading}
          </p>
        </div>
        {problems.length === 0 ? null : (
          <ul className="grid gap-2">
            {problems.map((problem) => (
              <RefusedFileRow key={problem.key} problem={problem} />
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

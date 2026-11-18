import { useId, useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ModalDialog } from '../components/sharing/ModalDialog';
import { templatesApi } from './api';
import { refusalText } from './list/refusalText';
import { TEMPLATES_TEST_IDS } from './testIds';
import type { TemplateRow } from './types';

export type NewTemplateDialogProps = {
  // The built-ins to start from, as the list sent them.
  builtIn: TemplateRow[];
  // The built-in picked when it opens (its id); the first one otherwise.
  initialBuiltIn?: string | null;
  onClose(): void;
};

const LABEL = 'text-xs font-semibold tracking-wider text-ink-muted uppercase';
const FAILED = 'Could not create the template. Try again.';

// "New template": a name and a built-in to start from. Create draft makes the draft and opens its
// editor. Rendered only while open; focus starts on the name, and Cancel or Esc put it back on the
// button that opened the dialog.
export function NewTemplateDialog({ builtIn, initialBuiltIn, onClose }: NewTemplateDialogProps) {
  const navigate = useNavigate();
  const titleId = useId();
  const hintId = useId();
  const errorId = useId();
  const nameRef = useRef<HTMLInputElement>(null);
  const [name, setName] = useState('');
  const [picked, setPicked] = useState(
    builtIn.find((row) => row.id === initialBuiltIn)?.id ?? builtIn[0]?.id ?? '',
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const created = await templatesApi.create({ basedOn: picked, name });
      navigate(`/templates/${encodeURIComponent(created.id)}/edit`);
    } catch (failure) {
      setSaving(false);
      setError(refusalText(failure) ?? FAILED);
      nameRef.current?.focus();
    }
  };

  return (
    <ModalDialog
      labelledBy={titleId}
      describedBy={hintId}
      testId={TEMPLATES_TEST_IDS.newDialog}
      widthClassName="max-w-lg"
      onClose={onClose}
    >
      <form className="grid max-h-[calc(100dvh-3rem)] overflow-y-auto" onSubmit={create}>
        <header className="grid gap-0.5 border-b border-edge px-5 py-3">
          <h2 id={titleId} className="text-base font-bold text-ink">
            New template
          </h2>
          <span id={hintId} className="text-meta text-ink-muted">
            Start from a built-in template, name it, and change what you like in the editor.
          </span>
        </header>
        <div className="grid gap-4 p-5">
          <label className="grid gap-1">
            <span className={LABEL}>Name</span>
            <input
              ref={nameRef}
              autoFocus
              required
              maxLength={60}
              aria-invalid={error !== null}
              aria-describedby={error === null ? undefined : errorId}
              className="field"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </label>
          <fieldset className="grid gap-1.5">
            <legend className={`mb-1 ${LABEL}`}>Start from</legend>
            {builtIn.map((row) => (
              <label
                key={row.id}
                className={`grid cursor-pointer grid-cols-[16px_minmax(0,1fr)] gap-2.5 rounded-md border px-3 py-2 ${
                  row.id === picked ? 'border-accent bg-accent-soft' : 'border-edge'
                }`}
              >
                <input
                  type="radio"
                  name="built-in"
                  value={row.id}
                  checked={row.id === picked}
                  onChange={() => setPicked(row.id)}
                  className="mt-1 accent-accent"
                />
                <span>
                  <b className="block font-semibold text-ink">{row.name}</b>
                  {row.purpose !== '' && (
                    <span className="block text-meta text-ink-muted">{row.purpose}</span>
                  )}
                </span>
              </label>
            ))}
          </fieldset>
          {error !== null && (
            <p id={errorId} role="alert" className="text-meta text-bad">
              {error}
            </p>
          )}
        </div>
        <footer className="flex justify-end gap-2 border-t border-edge px-5 py-3">
          <button type="button" className="btn-ghost" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn-primary" disabled={saving || picked === ''}>
            Create draft
          </button>
        </footer>
      </form>
    </ModalDialog>
  );
}

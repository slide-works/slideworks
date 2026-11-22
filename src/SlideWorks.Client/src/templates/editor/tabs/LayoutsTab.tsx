import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { templatesApi } from '../../api';
import { editorTabTestId } from '../../testIds';
import { LayoutUploader } from '../layouts/LayoutUploader';
import type { TemplateManifest } from '../../types';
import type { ChangeOutcome } from '../../useTemplateDraft';
import type { EditorTabProps } from '../tabProps';

type Status = 'checking' | 'Needs review' | 'Approved' | 'unknown';

// Whether the owner has approved the layout's current files, from the review the server keeps
// (the editor view does not carry it). Asked again when the layout's file changes, so a replaced
// layout reads "Needs review" at once.
function useApproval(templateId: string, name: string, file: string): Status {
  const [seen, setSeen] = useState<{ file: string; status: Status } | null>(null);
  useEffect(() => {
    let active = true;
    templatesApi.layoutReview(templateId, name).then(
      (review) =>
        active && setSeen({ file, status: review.approved ? 'Approved' : 'Needs review' }),
      () => active && setSeen({ file, status: 'unknown' }),
    );
    return () => {
      active = false;
    };
  }, [templateId, name, file]);
  return seen?.file === file ? seen.status : 'checking';
}

const STATUS_TEXT: Record<Status, string> = {
  checking: 'Checking…',
  'Needs review': 'Needs review',
  Approved: 'Approved',
  unknown: 'Status unknown',
};

function LayoutRow({
  templateId,
  name,
  file,
  onRemove,
}: {
  templateId: string;
  name: string;
  file: string;
  onRemove: () => void;
}) {
  const status = useApproval(templateId, name, file);
  return (
    <li
      data-testid="custom-layout-row"
      data-layout-name={name}
      className="flex flex-wrap items-center gap-3"
    >
      <span className="font-semibold text-ink">{name}</span>
      <span data-testid="custom-layout-status" className="text-meta text-ink-muted">
        {STATUS_TEXT[status]}
      </span>
      <Link
        to={`/templates/${encodeURIComponent(templateId)}/layouts/${encodeURIComponent(name)}`}
        data-layout-link={name}
        className="text-meta font-semibold text-accent underline"
      >
        {`Review ${name}`}
      </Link>
      <button
        type="button"
        className="btn-ghost"
        aria-label={`Remove layout ${name}`}
        onClick={onRemove}
      >
        Remove
      </button>
    </li>
  );
}

// The Custom layouts tab: the draft's layouts with their status and a link to each review page, an
// upload of the three files of one layout, and a remove button per layout (it saves the draft
// without the entry and leaves the stored file). Approving happens on the review page.
export function LayoutsTab({ draft, change }: EditorTabProps) {
  const id = useId();
  const layouts = draft.manifest.customLayouts;
  const section = useRef<HTMLElement>(null);
  const upload = useRef<HTMLButtonElement>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);
  // Where focus goes once the draft lists the layout that was just uploaded.
  const [focusLink, setFocusLink] = useState<string | null>(null);

  useEffect(() => {
    if (focusLink === null) return;
    const link = section.current?.querySelector<HTMLElement>(
      `[data-layout-link="${CSS.escape(focusLink)}"]`,
    );
    if (link) {
      link.focus();
      setFocusLink(null);
    }
  }, [focusLink, layouts]);

  // Removals and uploads change the one list, so they save one after another, each built from the
  // list the one before left (not from this render's): two quick removals both land, and an upload
  // never brings a removed layout back.
  const newest = useRef(layouts);
  const tail = useRef<Promise<unknown>>(Promise.resolve());
  useEffect(() => {
    newest.current = layouts;
  }, [layouts]);
  const save = useCallback(
    (update: (kept: TemplateManifest['customLayouts']) => TemplateManifest['customLayouts']) => {
      const run = tail.current.then(async (): Promise<ChangeOutcome> => {
        const next = update(newest.current);
        const outcome = await change({ customLayouts: next });
        if (outcome.ok) newest.current = next;
        return outcome;
      });
      tail.current = run.catch(() => undefined);
      return run;
    },
    [change],
  );

  async function remove(name: string) {
    setRemoveError(null);
    const outcome = await save((kept) => kept.filter((layout) => layout.name !== name));
    if (outcome.ok) upload.current?.focus();
    else setRemoveError(`Could not remove ${name}. Try again.`);
  }

  return (
    <section
      ref={section}
      data-testid={editorTabTestId('layouts')}
      aria-label={`Custom layouts of ${draft.name}`}
      className="panel grid gap-4 p-4"
    >
      <h2 id={`${id}-heading`} className="text-sm font-semibold text-ink">
        Custom layouts
      </h2>
      <div className="grid gap-2">
        <h3 id={`${id}-list`} className="text-meta font-semibold text-ink-muted">
          Layouts in this template
        </h3>
        {layouts.length === 0 ? (
          <p className="text-meta text-ink-muted">No custom layouts yet.</p>
        ) : (
          <ul aria-labelledby={`${id}-list`} className="grid gap-2">
            {layouts.map((layout) => (
              <LayoutRow
                key={layout.name}
                templateId={draft.id}
                name={layout.name}
                file={layout.file}
                onRemove={() => void remove(layout.name)}
              />
            ))}
          </ul>
        )}
        {removeError === null ? null : (
          <p role="alert" className="text-meta text-bad">
            {removeError}
          </p>
        )}
      </div>
      <LayoutUploader
        draft={draft}
        change={change}
        save={save}
        buttonRef={upload}
        onAccepted={setFocusLink}
      />
    </section>
  );
}

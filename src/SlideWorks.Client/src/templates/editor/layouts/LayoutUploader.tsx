import { useEffect, useId, useRef, useState, type ChangeEvent, type Ref } from 'react';
import type { ApiFieldError } from '../../../api/client';
import type { ChangeOutcome } from '../../useTemplateDraft';
import type { TemplateEditorView, TemplateManifest } from '../../types';
import { uploadAsset } from '../uploadAsset';

const WRONG_FILES = 'Choose one .html, one .css and one .json file.';
const NETWORK = 'Could not upload the layout. Try again.';

// What the person sees when a layout was not taken: the heading's subject (the layout's name, or
// the .json's file name when it names none) and what is wrong, each with the file position it is
// about when it has one. Always drawn as text.
type Refusal = { subject: string; findings: { path: string | null; message: string }[] };

type Files = { html: File; css: File; json: File };

// One .html, one .css and one .json from what was chosen, or null when it is any other set.
function threeFiles(chosen: File[]): Files | null {
  if (chosen.length !== 3) return null;
  const ending = (suffix: string) =>
    chosen.filter((file) => file.name.toLowerCase().endsWith(suffix));
  const [html, css, json] = [ending('.html'), ending('.css'), ending('.json')];
  return html.length === 1 && css.length === 1 && json.length === 1
    ? { html: html[0], css: css[0], json: json[0] }
    : null;
}

export type LayoutUploaderProps = {
  draft: TemplateEditorView;
  change(patch: Partial<TemplateManifest>): Promise<ChangeOutcome>;
  // The layout the files are for, on the review page: the .json must name it. Left out, any name.
  replacing?: string;
  // The files were accepted and the draft lists the layout: its name.
  onAccepted(name: string): void;
  // Saves the layouts list built from the newest one when the save runs, so a save that overlaps
  // another never builds from a list that is out of date. Left out, the draft's list is used.
  save?(
    update: (kept: TemplateManifest['customLayouts']) => TemplateManifest['customLayouts'],
  ): Promise<ChangeOutcome>;
  buttonRef?: Ref<HTMLButtonElement>;
};

// Upload (or replace) one custom layout from three chosen files. The browser builds one JSON
// document { markup, css, slots } from them (the .json is parsed here: a parse failure is a refusal
// with no call), uploads it as kind layout, and on acceptance saves the draft with { name, file } in
// the manifest's customLayouts, replacing an entry of the same name. The sanitiser's refusals,
// failures and a wrong set of files show in a region with role "alert" whose heading takes focus.
export function LayoutUploader({
  draft,
  change,
  replacing,
  onAccepted,
  save,
  buttonRef,
}: LayoutUploaderProps) {
  const id = useId();
  const picker = useRef<HTMLInputElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const [busy, setBusy] = useState('');
  const [refusal, setRefusal] = useState<Refusal | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [focusAlert, setFocusAlert] = useState(0);

  useEffect(() => {
    if (focusAlert > 0) heading.current?.focus();
  }, [focusAlert]);

  const refuse = (next: Refusal) => {
    setRefusal(next);
    setFocusAlert((count) => count + 1);
  };

  async function choose(event: ChangeEvent<HTMLInputElement>) {
    const chosen = Array.from(event.target.files ?? []);
    event.target.value = '';
    if (chosen.length === 0) return;
    setRefusal(null);
    setMessage(null);
    const files = threeFiles(chosen);
    if (files === null) {
      setMessage(WRONG_FILES);
      return;
    }
    setBusy('Uploading the layout');
    try {
      await send(files);
    } finally {
      setBusy('');
    }
  }

  async function send({ html, css, json }: Files) {
    const [markup, style, slotsText] = await Promise.all([html.text(), css.text(), json.text()]);
    let slots: unknown;
    try {
      slots = JSON.parse(slotsText);
    } catch {
      refuse({
        subject: json.name,
        findings: [{ path: json.name, message: 'This file is not valid JSON.' }],
      });
      return;
    }
    const name = nameOf(slots);
    if (name === null) {
      refuse({
        subject: json.name,
        findings: [{ path: json.name, message: 'The "name" of the layout is missing.' }],
      });
      return;
    }
    if (replacing !== undefined && name !== replacing) {
      refuse({
        subject: name,
        findings: [
          { path: json.name, message: `These files are for "${name}", not for "${replacing}".` },
        ],
      });
      return;
    }
    setBusy(`Uploading ${name}`);
    const body = new File([JSON.stringify({ markup, css: style, slots })], `${name}.json`, {
      type: 'application/json',
    });
    const outcome = await uploadAsset(draft.id, 'layout', body, false);
    if (!outcome.ok) {
      if (outcome.kind === 'refused') {
        refuse({ subject: name, findings: findingsOf(outcome.errors, outcome.message) });
      } else {
        setMessage(outcome.kind === 'unreachable' ? NETWORK : outcome.message);
      }
      return;
    }
    const entry = { name, file: outcome.asset.assetId };
    const update = (kept: TemplateManifest['customLayouts']) =>
      kept.some((layout) => layout.name === name)
        ? kept.map((layout) => (layout.name === name ? entry : layout))
        : [...kept, entry];
    const saved = await (
      save ?? ((build) => change({ customLayouts: build(draft.manifest.customLayouts) }))
    )(update);
    if (!saved.ok) {
      setMessage(saved.message);
      return;
    }
    onAccepted(name);
  }

  const label =
    replacing === undefined ? 'Upload a custom layout' : `Replace files for ${replacing}`;
  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap items-center gap-3">
        <button
          ref={buttonRef}
          type="button"
          className="btn-ghost"
          disabled={busy !== ''}
          aria-label={replacing === undefined ? undefined : label}
          onClick={() => picker.current?.click()}
        >
          {replacing === undefined ? label : 'Replace files…'}
        </button>
        <input
          ref={picker}
          type="file"
          multiple
          accept=".html,.css,.json"
          tabIndex={-1}
          aria-hidden="true"
          className="sr-only"
          data-testid="layout-files"
          onChange={(event) => void choose(event)}
        />
        <p role="status" className="text-meta text-ink-muted">
          {busy}
        </p>
      </div>
      {message === null ? null : (
        <p role="alert" className="text-meta text-bad">
          {message}
        </p>
      )}
      {refusal === null ? null : (
        <section role="alert" aria-labelledby={`${id}-refused`} className="grid gap-1">
          <h3
            ref={heading}
            id={`${id}-refused`}
            tabIndex={-1}
            className="text-meta font-semibold text-bad"
          >
            {`${refusal.subject} was not accepted`}
          </h3>
          <ul className="grid gap-1" data-testid="layout-refusals">
            {refusal.findings.map((finding, index) => (
              <li key={index} className="text-meta text-ink">
                {finding.path === null ? null : (
                  <code className="mr-2 font-mono text-ink-muted">{finding.path}</code>
                )}
                <span>{finding.message}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

function nameOf(slots: unknown): string | null {
  if (typeof slots !== 'object' || slots === null) return null;
  const name = (slots as Record<string, unknown>).name;
  return typeof name === 'string' && name !== '' ? name : null;
}

const findingsOf = (errors: ApiFieldError[], message: string): Refusal['findings'] =>
  errors.length === 0
    ? [{ path: null, message }]
    : errors.map(({ path, message: text }) => ({ path, message: text }));

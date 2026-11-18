import { useId, useRef, useState, type ReactNode } from 'react';
import type { FooterMode } from '../../../api/types';
import { LAYOUT_NAMES } from '../../../slides/slots';
import { editorTabTestId } from '../../testIds';
import type { TemplateManifest } from '../../types';
import type { ChangeOutcome } from '../../useTemplateDraft';
import { fieldOfPath, linesOf, omit, type BasicsField } from '../basics/fields';
import { SHAPE_CHOICES, shapeChoiceOf, shapeFor } from '../basics/shapeChoices';
import type { EditorTabProps } from '../tabProps';

// join, poll and qa are allowed in every template, so they are not chips.
const ALWAYS = ['join', 'poll', 'qa'];
const CHIPS = LAYOUT_NAMES.filter((name) => !ALWAYS.includes(name));

const FOOTERS: { value: FooterMode; label: string }[] = [
  { value: 'author', label: 'Footer shows the author' },
  { value: 'date', label: 'Footer shows the date' },
  { value: 'off', label: 'No footer' },
];

const CHROME_TICKS: { field: BasicsField; key: keyof TemplateManifest['chrome']; label: string }[] =
  [
    { field: 'tracker', key: 'tracker', label: 'Part tracker' },
    { field: 'progress', key: 'progress', label: 'Progress bar' },
    { field: 'deepDivesOnDivider', key: 'deepDivesOnDivider', label: 'Dividers list deep dives' },
    { field: 'draft', key: 'draft', label: 'Draft stamp on new decks' },
  ];

type TextField = 'label' | 'name' | 'purpose' | 'outline' | 'houseStyle';
const isList = (field: TextField) => field === 'outline' || field === 'houseStyle';

// What is on screen ahead of the server: structured fields that were changed and not yet read back.
type Pending = Partial<Pick<TemplateManifest, 'chrome' | 'layouts' | 'shape'>>;

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

// The Basics tab: name, short name, purpose, the layouts allowed, chrome defaults, the footer, the
// outline and house style, and the corner shape. A text field saves when it loses focus, a tick, a
// chip or a choice at once. The form is one stable tree: a save never re-mounts it, so focus stays
// where the person is. A refused field keeps what was typed, shows the server's sentence beside it
// and takes focus; a failure with no field (the server could not be reached, a stale revision) is
// shown by the editor's frame, which also owns Retry and Reload.
export function BasicsTab({ draft, change }: EditorTabProps) {
  const manifest = draft.manifest;
  const id = useId();
  const [typed, setTyped] = useState<Partial<Record<TextField, string>>>({});
  const [pending, setPending] = useState<Pending>({});
  const [errors, setErrors] = useState<Partial<Record<BasicsField, string>>>({});
  const pendingRef = useRef<Pending>({});
  const sent = useRef<Partial<Record<string, string>>>({});
  // The last value the server refused for a text field: not sent again until the text changes.
  const refused = useRef<Partial<Record<string, string>>>({});
  // Structured values whose save got no answer: the frame sends them again under the next change.
  const failedKeys = useRef<Pending>({});

  const chrome = pending.chrome ?? manifest.chrome;
  const layouts = pending.layouts ?? manifest.layouts;
  const shape = pending.shape ?? manifest.shape;

  const textOf = (field: TextField): string => {
    if (typed[field] !== undefined) return typed[field];
    const value = manifest[field];
    return typeof value === 'string' ? value : value.join('\n');
  };

  const clearError = (field: BasicsField) =>
    setErrors((current) => {
      if (current[field] === undefined) return current;
      return omit(current, field);
    });

  const setPend = (next: Pending) => {
    pendingRef.current = next;
    setPending(next);
  };

  // What came of a save: forget what was on screen ahead of the server, or say why not.
  const settle =
    (key: keyof TemplateManifest, value: unknown, carried: Pending) => (outcome: ChangeOutcome) => {
      delete sent.current[key];
      // The held-over values this save carried: it answered, so they are saved or refused with it.
      const forgetCarried = () => {
        let next = pendingRef.current;
        for (const name of Object.keys(carried) as (keyof Pending)[]) {
          if (same(next[name], carried[name])) next = omit(next, name);
          failedKeys.current = omit(failedKeys.current, name);
        }
        if (next !== pendingRef.current) setPend(next);
      };
      if (outcome.ok) {
        forgetCarried();
        if (key === 'chrome' || key === 'layouts' || key === 'shape') {
          if (same(pendingRef.current[key], value)) {
            setPend(omit(pendingRef.current, key));
          }
        } else {
          const text = key as TextField;
          setTyped((current) => {
            const raw = current[text];
            if (raw === undefined || !same(isList(text) ? linesOf(raw) : raw, value))
              return current;
            return omit(current, text);
          });
        }
        return;
      }
      if (outcome.code === 'stale_revision') {
        // Typed text stays until the person has reloaded: it is theirs, not the server's.
        setPend({});
        setErrors({});
        return;
      }
      if (outcome.code === null) {
        if (key === 'chrome' || key === 'layouts' || key === 'shape') {
          failedKeys.current = { ...failedKeys.current, [key]: value };
        }
        return;
      }
      forgetCarried();
      if (outcome.code !== null && (key === 'chrome' || key === 'layouts' || key === 'shape')) {
        // Refused: the frame draws the saved value again, so this tab does too.
        if (same(pendingRef.current[key], value)) setPend(omit(pendingRef.current, key));
      } else if (outcome.code !== null) refused.current[key] = JSON.stringify(value);
      const at = fieldOfPath(outcome.path);
      if (at !== null) {
        setErrors((current) => ({ ...current, [at]: outcome.message }));
        document.getElementById(`${id}-${at}`)?.focus();
      }
    };

  const save = (field: BasicsField, key: keyof TemplateManifest, value: unknown) => {
    clearError(field);
    sent.current[key] = JSON.stringify(value);
    delete refused.current[key];
    const carried = { ...failedKeys.current };
    void change({ [key]: value } as Partial<TemplateManifest>).then(settle(key, value, carried));
  };

  // Text: sent when the field loses focus and the text is not what the draft already holds.
  const commitText = (field: TextField) => {
    const raw = typed[field];
    if (raw === undefined) return;
    const value = isList(field) ? linesOf(raw) : raw;
    if (same(value, manifest[field])) {
      setTyped((current) => omit(current, field));
      return;
    }
    if (sent.current[field] === JSON.stringify(value)) return;
    if (refused.current[field] === JSON.stringify(value)) return;
    save(field, field, value);
  };

  const apply = (field: BasicsField, key: 'chrome' | 'layouts' | 'shape', value: unknown) => {
    setPend({ ...pendingRef.current, [key]: value });
    save(field, key, value);
  };

  const tick = (field: BasicsField, key: keyof TemplateManifest['chrome'], value: unknown) =>
    apply(field, 'chrome', { ...(pendingRef.current.chrome ?? manifest.chrome), [key]: value });

  const toggleLayout = (name: string) => {
    const current = pendingRef.current.layouts ?? manifest.layouts;
    const next = current.includes(name)
      ? current.filter((existing) => existing !== name)
      : [...current, name];
    // Chips keep the catalogue's order; a name the catalogue does not know stays at the end.
    const order = (layout: string) => {
      const at = (LAYOUT_NAMES as readonly string[]).indexOf(layout);
      return at === -1 ? LAYOUT_NAMES.length : at;
    };
    apply(
      'layouts',
      'layouts',
      [...next].sort((a, b) => order(a) - order(b)),
    );
  };

  const described = (field: BasicsField, ...extra: string[]) =>
    [...extra, errors[field] === undefined ? '' : `${id}-${field}-error`]
      .filter((part) => part !== '')
      .join(' ') || undefined;
  const invalid = (field: BasicsField) => (errors[field] === undefined ? undefined : true);
  const errorOf = (field: BasicsField) =>
    errors[field] === undefined ? null : (
      <p id={`${id}-${field}-error`} className="text-meta text-bad">
        {errors[field]}
      </p>
    );

  const textInput = (field: 'label' | 'name' | 'purpose', title: string, hint?: ReactNode) => (
    <div className="grid gap-1">
      <label htmlFor={`${id}-${field}`} className="text-meta font-semibold text-ink-muted">
        {title}
      </label>
      <input
        id={`${id}-${field}`}
        type="text"
        className="field"
        value={textOf(field)}
        aria-invalid={invalid(field)}
        aria-describedby={described(field, hint ? `${id}-${field}-hint` : '')}
        onChange={(event) => setTyped((current) => ({ ...current, [field]: event.target.value }))}
        onBlur={() => commitText(field)}
      />
      {hint && (
        <p id={`${id}-${field}-hint`} className="text-meta text-ink-muted">
          {hint}
        </p>
      )}
      {errorOf(field)}
    </div>
  );

  const textArea = (field: 'outline' | 'houseStyle', title: string, hint: string) => (
    <div className="grid gap-1">
      <label htmlFor={`${id}-${field}`} className="text-meta font-semibold text-ink-muted">
        {title}
      </label>
      <textarea
        id={`${id}-${field}`}
        rows={5}
        className="field"
        value={textOf(field)}
        aria-invalid={invalid(field)}
        aria-describedby={described(field, `${id}-${field}-hint`)}
        onChange={(event) => setTyped((current) => ({ ...current, [field]: event.target.value }))}
        onBlur={() => commitText(field)}
      />
      <p id={`${id}-${field}-hint`} className="text-meta text-ink-muted">
        {hint}
      </p>
      {errorOf(field)}
    </div>
  );

  const choice = shapeChoiceOf(shape);
  return (
    <section
      data-testid={editorTabTestId('basics')}
      aria-labelledby={`${id}-heading`}
      className="panel grid gap-4 p-4"
    >
      <h2 id={`${id}-heading`} className="text-sm font-semibold text-ink">
        Basics
      </h2>

      <div className="grid gap-4 sm:grid-cols-2">
        {textInput('label', 'Name')}
        {textInput('name', 'Short name', 'Shown to agents in list_templates.')}
      </div>
      {textInput('purpose', 'What it is for')}

      <fieldset className="grid gap-2">
        <legend className="text-meta font-semibold text-ink-muted">Layouts allowed</legend>
        <div className="flex flex-wrap gap-2">
          {CHIPS.map((name, index) => (
            <button
              key={name}
              type="button"
              id={index === 0 ? `${id}-layouts` : undefined}
              aria-pressed={layouts.includes(name)}
              aria-describedby={described('layouts')}
              data-testid={`template-layout-chip-${name}`}
              className={`rounded-full border px-2.5 py-0.5 text-meta ${
                layouts.includes(name)
                  ? 'border-accent bg-accent-light text-ink'
                  : 'border-edge text-ink-muted'
              }`}
              onClick={() => toggleLayout(name)}
            >
              {name}
            </button>
          ))}
        </div>
        <p className="text-meta text-ink-muted">join and poll are always allowed.</p>
        {errorOf('layouts')}
      </fieldset>

      <fieldset className="grid gap-1 text-meta">
        <legend className="font-semibold text-ink-muted">Chrome defaults</legend>
        {CHROME_TICKS.map(({ field, key, label }) => (
          <div key={key}>
            <label>
              <input
                id={`${id}-${field}`}
                type="checkbox"
                checked={chrome[key] === true}
                aria-invalid={invalid(field)}
                aria-describedby={described(field)}
                onChange={(event) => tick(field, key, event.target.checked)}
              />{' '}
              {label}
            </label>
            {errorOf(field)}
          </div>
        ))}
        <div>
          <label>
            <input
              id={`${id}-topBar`}
              type="checkbox"
              checked={chrome.topBar === 'accent'}
              aria-invalid={invalid('topBar')}
              aria-describedby={described('topBar')}
              onChange={(event) =>
                tick('topBar', 'topBar', event.target.checked ? 'accent' : 'none')
              }
            />{' '}
            Accent bar across the top
          </label>
          {errorOf('topBar')}
        </div>
        <div className="grid gap-1">
          <label htmlFor={`${id}-footer`} className="sr-only">
            Footer
          </label>
          <select
            id={`${id}-footer`}
            className="field"
            value={chrome.footer}
            aria-invalid={invalid('footer')}
            aria-describedby={described('footer')}
            onChange={(event) => tick('footer', 'footer', event.target.value as FooterMode)}
          >
            {FOOTERS.map(({ value, label }) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
          {errorOf('footer')}
        </div>
      </fieldset>

      {textArea('outline', 'Outline', 'One line each. Agents get these back from create_deck.')}
      {textArea('houseStyle', 'House style', 'One line each. The rules agents write by.')}

      <fieldset className="grid gap-1">
        <legend className="text-meta font-semibold text-ink-muted">Shape</legend>
        <div className="flex gap-4 text-meta">
          {SHAPE_CHOICES.map(({ radius, label }) => (
            <label key={radius}>
              <input
                type="radio"
                name={`${id}-shape`}
                id={radius === (choice ?? SHAPE_CHOICES[0].radius) ? `${id}-shape` : undefined}
                checked={choice === radius}
                aria-describedby={described('shape')}
                onChange={() => apply('shape', 'shape', shapeFor(radius, shape.shadow))}
              />{' '}
              {label}
            </label>
          ))}
          {choice === null && (
            <label>
              <input type="radio" name={`${id}-shape`} checked disabled readOnly /> Custom (
              {shape.radius.md} px)
            </label>
          )}
        </div>
        <p className="text-meta text-ink-muted">
          Corner radius of cards and panels. Choosing a size sets all four corners. The slide scale
          is fixed for every template.
        </p>
        {errorOf('shape')}
      </fieldset>
    </section>
  );
}

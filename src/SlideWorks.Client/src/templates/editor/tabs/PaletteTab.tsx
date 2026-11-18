import palettesJson from '@contracts/palettes.json' with { type: 'json' };
import { useId, useRef, useState } from 'react';
import type { PaletteTokens } from '../../../api/types';
import { editorTabTestId } from '../../testIds';
import type { TemplateExtraColour, TemplateManifest, TemplateManifestPalette } from '../../types';
import {
  EXTRA_INK_NEEDED,
  checkPairs,
  contrastRatio,
  fixFor,
  isHexColour,
  nearestPassing,
  ratioText,
} from '../contrast';
import { omit } from '../basics/fields';
import type { EditorTabProps } from '../tabProps';

const BUILT_INS = palettesJson.palettes;
const HEX_HELP = 'Use a colour like #0F766E.';
const MAX_EXTRAS = 4;
const EXTRA_NAMES: TemplateExtraColour['name'][] = ['extra-1', 'extra-2', 'extra-3', 'extra-4'];
const NONE = 'none';
const CUSTOM = 'custom';

// The token keys in groups, accent-2 beside accent. A key a group does not list lands in "Other",
// so a new token is never left without an input.
const GROUPS: { title: string; keys: string[] }[] = [
  { title: 'Surfaces', keys: ['paper', 'card', 'chip', 'grid'] },
  { title: 'Text', keys: ['ink', 'muted'] },
  {
    title: 'Accent',
    keys: [
      'accent',
      'accent-2',
      'accent-dark',
      'accent-light',
      'accent-glow',
      'accent-past',
      'on-accent',
    ],
  },
  {
    title: 'Good, bad and warning',
    keys: [
      'good',
      'good-soft',
      'good-ink',
      'bad',
      'bad-soft',
      'bad-ink',
      'warn',
      'warn-soft',
      'warn-ink',
    ],
  },
  { title: 'Deep dives', keys: ['dd', 'on-dd', 'dd-on-night'] },
  {
    title: 'Dark slides',
    keys: ['night', 'night-ink', 'night-2', 'night-muted', 'night-line', 'night-glow'],
  },
  {
    title: 'Panels',
    keys: [
      'panel-accent',
      'panel-accent-ink',
      'panel-positive',
      'panel-positive-ink',
      'panel-negative',
      'panel-negative-ink',
      'panel-inverse',
      'panel-inverse-ink',
    ],
  },
];

const TOKEN_KEYS = Object.keys(BUILT_INS[0]!.tokens);
const LISTED = new Set(GROUPS.flatMap((group) => group.keys));
const OTHER = TOKEN_KEYS.filter((key) => !LISTED.has(key));
const ALL_GROUPS = OTHER.length === 0 ? GROUPS : [...GROUPS, { title: 'Other', keys: OTHER }];

// "deep-dive-pill" read as words: "Deep-dive pill".
const pairLabel = (name: string) => {
  const words = name.replace(/-/g, ' ').replace(/^deep dive/, 'deep-dive');
  return words.charAt(0).toUpperCase() + words.slice(1);
};

// A colour that may go into a style: only a validated 6-digit hex, never a typed string.
const safe = (value: string | undefined): string | undefined =>
  value !== undefined && isHexColour(value) ? value : undefined;

const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

type Extras = TemplateExtraColour[];
// The palette field a refusal's path names: `palette.accent` -> `accent`, `palette.extras[1].ink`
// -> `extras.1.ink`. The inputs' ids are built the same way.
const fieldOfPath = (path: string | undefined): string | null => {
  if (path === undefined || !path.startsWith('palette.')) return null;
  return path
    .slice('palette.'.length)
    .replace(/\[(\d+)\]/g, '.$1')
    .replace(/^extras$/, 'extras');
};

function Swatch({ colour }: { colour: string | undefined }) {
  return (
    <span
      aria-hidden="true"
      className="inline-block h-5 w-5 shrink-0 rounded border border-edge"
      style={colour === undefined ? undefined : { background: colour }}
    />
  );
}

type ColourFieldProps = {
  id: string;
  label: string;
  raw: string;
  saved: string;
  error: string | undefined;
  onChange(value: string): void;
};

// One colour: a swatch and a text input. Anything that is not 6-digit hex says how to write one.
function ColourField({ id, label, raw, saved, error, onChange }: ColourFieldProps) {
  const valid = isHexColour(raw);
  const text = valid ? error : HEX_HELP;
  return (
    <div className="grid gap-0.5">
      <label htmlFor={id} className="text-meta text-ink-muted">
        {label}
      </label>
      <div className="flex items-center gap-2">
        <Swatch colour={safe(valid ? raw : saved)} />
        <input
          id={id}
          type="text"
          className="field w-28 font-mono"
          spellCheck={false}
          autoComplete="off"
          value={raw}
          aria-invalid={text === undefined ? undefined : true}
          aria-describedby={text === undefined ? undefined : `${id}-error`}
          onChange={(event) => onChange(event.target.value)}
        />
      </div>
      {text !== undefined && (
        <p id={`${id}-error`} className="text-meta text-bad">
          {text}
        </p>
      )}
    </div>
  );
}

// The Palette tab: start from a built-in palette, then one colour input per token in groups, up to
// four extra colours with an ink each, and a lock. Every colour that is valid hex is saved at once
// and checked against the server's own contrast pairs as it changes; a failing pair shows its
// ratio, what it needs and a button that offers the nearest passing colour. Nothing changes a
// colour but a click or typing. What was typed stays until the server has it: a refused colour
// keeps its text and its message, and takes focus.
export function PaletteTab({ draft, change }: EditorTabProps) {
  const id = useId();
  const manifestPalette = draft.manifest.palette;
  const [typed, setTyped] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  // What was last handed to change(): the next change builds on it, so two quick edits both land.
  const working = useRef<TemplateManifestPalette | undefined>(undefined);
  const refused = useRef<Record<string, string>>({});

  const inputId = (field: string) => `${id}-${field}`;
  const focusField = (field: string) => document.getElementById(inputId(field))?.focus();

  const palette = manifestPalette;
  const extras: Extras = palette?.extras ?? [];

  // The values on screen: what the draft holds with every valid typed colour on top, so contrast
  // answers before the save does.
  const valueOf = (field: string, saved: string): string => typed[field] ?? saved;
  const tokens: Record<string, string> | null =
    palette === undefined
      ? null
      : Object.fromEntries(
          TOKEN_KEYS.map((key) => {
            const shown = valueOf(key, palette[key as keyof PaletteTokens]);
            return [key, isHexColour(shown) ? shown : palette[key as keyof PaletteTokens]];
          }),
        );
  const extraOf = (index: number): TemplateExtraColour => {
    const extra = extras[index]!;
    const colour = valueOf(`extras.${index}.colour`, extra.colour);
    const ink = valueOf(`extras.${index}.ink`, extra.ink);
    return {
      name: extra.name,
      colour: isHexColour(colour) ? colour : extra.colour,
      ink: isHexColour(ink) ? ink : extra.ink,
    };
  };

  const send = (field: string, patch: Partial<TemplateManifest>) => {
    void change(patch).then((outcome) => {
      if (outcome.ok) {
        setTyped((current) => {
          if (current[field] === undefined) return current;
          return omit(current, field);
        });
        return;
      }
      if (outcome.code === 'stale_revision') {
        setTyped({});
        setErrors({});
        working.current = undefined;
        return;
      }
      if (outcome.code === null) return;
      working.current = undefined;
      const at = fieldOfPath(outcome.path) ?? field;
      refused.current[at] = typed[at] ?? '';
      setErrors((current) => ({ ...current, [at]: outcome.message }));
      focusField(at);
    });
  };

  // Builds the palette with one token or extra changed, on top of what was sent last.
  const build = (edit: (base: TemplateManifestPalette) => TemplateManifestPalette) => {
    const base =
      working.current !== undefined &&
      manifestPalette !== undefined &&
      !same(working.current, manifestPalette)
        ? working.current
        : manifestPalette;
    const next = edit(base ?? (BUILT_INS[0]!.tokens as TemplateManifestPalette));
    working.current = next;
    return next;
  };

  const clearError = (field: string) =>
    setErrors((current) => {
      if (current[field] === undefined) return current;
      return omit(current, field);
    });

  const setColour = (field: string, raw: string, apply: (value: string) => void) => {
    setTyped((current) => ({ ...current, [field]: raw }));
    clearError(field);
    delete refused.current[field];
    if (isHexColour(raw)) apply(raw);
  };

  const setToken = (key: string, value: string) => {
    const next = build((base) => ({ ...base, [key]: value }));
    send(key, { palette: next });
  };
  const setExtra = (index: number, part: 'colour' | 'ink', value: string) => {
    const next = build((base) => ({
      ...base,
      extras: (base.extras ?? []).map((extra, i) =>
        i === index ? { ...extra, [part]: value } : extra,
      ),
    }));
    send(`extras.${index}.${part}`, { palette: next });
  };

  const choose = (value: string) => {
    if (value === CUSTOM) return;
    working.current = undefined;
    setTyped({});
    setErrors({});
    if (value === NONE) {
      void change({ palette: undefined, lockPalette: false }).then((outcome) => {
        if (!outcome.ok && outcome.code !== null && outcome.code !== 'stale_revision') {
          setErrors({ palette: outcome.message });
        }
      });
      return;
    }
    const builtIn = BUILT_INS.find((candidate) => candidate.name === value);
    if (!builtIn) return;
    const next: TemplateManifestPalette = {
      ...(builtIn.tokens as PaletteTokens),
      ...(extras.length > 0 ? { extras } : {}),
    };
    working.current = next;
    void change({ palette: next }).then((outcome) => {
      if (!outcome.ok && outcome.code !== null && outcome.code !== 'stale_revision') {
        setErrors({ palette: outcome.message });
        focusField('palette');
      }
    });
  };

  const addExtra = () => {
    const name = EXTRA_NAMES.find((candidate) => !extras.some((extra) => extra.name === candidate));
    if (name === undefined || palette === undefined) return;
    const next = build((base) => ({
      ...base,
      extras: [
        ...(base.extras ?? []),
        { name, colour: tokens!.accent!, ink: tokens!['on-accent']! },
      ],
    }));
    send('extras', { palette: next });
  };
  const removeExtra = (index: number) => {
    const next = build((base) => {
      const rest = (base.extras ?? []).filter((_, i) => i !== index);
      const tokensOnly = omit(base, 'extras');
      return rest.length === 0 ? tokensOnly : { ...tokensOnly, extras: rest };
    });
    setTyped((current) =>
      Object.fromEntries(Object.entries(current).filter(([field]) => !field.startsWith('extras.'))),
    );
    send('extras', { palette: next });
  };

  const applyFix = (token: string, colour: string) => {
    setToken(token, colour);
    setTyped((current) => ({ ...current, [token]: colour }));
    focusField(token);
  };

  const current =
    palette === undefined
      ? NONE
      : (BUILT_INS.find((candidate) =>
          TOKEN_KEYS.every(
            (key) =>
              candidate.tokens[key as keyof typeof candidate.tokens] ===
              palette[key as keyof PaletteTokens],
          ),
        )?.name ?? CUSTOM);

  const failing = tokens === null ? [] : checkPairs(tokens).filter((check) => !check.pass);

  return (
    <section
      data-testid={editorTabTestId('palette')}
      aria-labelledby={`${id}-heading`}
      className="panel grid gap-4 p-4"
    >
      <h2 id={`${id}-heading`} className="text-sm font-semibold text-ink">
        Palette
      </h2>

      <div className="grid gap-1">
        <label htmlFor={inputId('palette')} className="text-meta font-semibold text-ink-muted">
          Start from a built-in palette
        </label>
        <select
          id={inputId('palette')}
          className="field"
          value={current}
          aria-invalid={errors.palette === undefined ? undefined : true}
          aria-describedby={errors.palette === undefined ? undefined : `${id}-palette-error`}
          onChange={(event) => choose(event.target.value)}
        >
          <option value={NONE}>No palette of its own</option>
          {current === CUSTOM && (
            <option value={CUSTOM} disabled>
              Custom
            </option>
          )}
          {BUILT_INS.map((builtIn) => (
            <option key={builtIn.name} value={builtIn.name}>
              {builtIn.label}
            </option>
          ))}
        </select>
        <p className="text-meta text-ink-muted">
          {palette === undefined
            ? 'Without a palette of its own, a deck keeps the palette it picked. Choose one to edit its colours.'
            : 'Choosing a palette replaces every colour below. Your extra colours stay.'}
        </p>
        {errors.palette !== undefined && (
          <p id={`${id}-palette-error`} className="text-meta text-bad">
            {errors.palette}
          </p>
        )}
      </div>

      {palette !== undefined && tokens !== null && (
        <>
          <div data-testid="palette-contrast" aria-live="polite" className="grid gap-2">
            <p
              className={`text-meta font-semibold ${failing.length === 0 ? 'text-ok' : 'text-bad'}`}
            >
              {failing.length === 0
                ? 'Every contrast pair passes.'
                : `${failing.length} contrast ${failing.length === 1 ? 'pair fails' : 'pairs fail'}.`}
            </p>
            {failing.map((check) => {
              const fix = fixFor(check, tokens);
              return (
                <div
                  key={check.pair.name}
                  data-testid="palette-failing-pair"
                  className="flex flex-wrap items-center gap-2 rounded-md border border-edge p-2 text-meta"
                >
                  <span className="rounded-full bg-bad-soft px-2 py-0.5 font-semibold text-bad-ink">
                    {ratioText(check.ratio)}
                  </span>
                  <span>
                    {pairLabel(check.pair.name)} needs {check.pair.needed.toFixed(1)}:1
                  </span>
                  <span
                    aria-hidden="true"
                    className="rounded px-2 py-0.5 font-semibold"
                    style={{ background: safe(check.ground), color: safe(check.fore) }}
                  >
                    Aa
                  </span>
                  {fix !== null && (
                    <button
                      type="button"
                      className="btn-ghost"
                      aria-label={`Use ${fix.colour.toUpperCase()} for the ${pairLabel(check.pair.name).toLowerCase()}`}
                      onClick={() => applyFix(fix.token, fix.colour)}
                    >
                      Use {fix.colour.toUpperCase()}
                    </button>
                  )}
                </div>
              );
            })}
          </div>

          {ALL_GROUPS.map((group) => (
            <fieldset key={group.title} className="grid gap-2">
              <legend className="text-meta font-semibold text-ink-muted">{group.title}</legend>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                {group.keys.map((key) => (
                  <ColourField
                    key={key}
                    id={inputId(key)}
                    label={key}
                    raw={valueOf(key, palette[key as keyof PaletteTokens])}
                    saved={palette[key as keyof PaletteTokens]}
                    error={errors[key]}
                    onChange={(value) => setColour(key, value, (hex) => setToken(key, hex))}
                  />
                ))}
              </div>
            </fieldset>
          ))}

          <fieldset className="grid gap-2">
            <legend className="text-meta font-semibold text-ink-muted">Extra colours</legend>
            {extras.map((_, index) => {
              const extra = extraOf(index);
              const ratio = contrastRatio(extra.ink, extra.colour);
              const fix =
                ratio < EXTRA_INK_NEEDED
                  ? nearestPassing(extra.ink, extra.colour, EXTRA_INK_NEEDED)
                  : null;
              return (
                <div key={extra.name} data-testid="palette-extra" className="grid gap-1">
                  <div className="flex flex-wrap items-end gap-3">
                    <span className="text-meta font-semibold text-ink">{extra.name}</span>
                    <ColourField
                      id={inputId(`extras.${index}.colour`)}
                      label={`${extra.name} colour`}
                      raw={valueOf(`extras.${index}.colour`, extras[index]!.colour)}
                      saved={extras[index]!.colour}
                      error={errors[`extras.${index}.colour`]}
                      onChange={(value) =>
                        setColour(`extras.${index}.colour`, value, (hex) =>
                          setExtra(index, 'colour', hex),
                        )
                      }
                    />
                    <ColourField
                      id={inputId(`extras.${index}.ink`)}
                      label={`${extra.name} ink`}
                      raw={valueOf(`extras.${index}.ink`, extras[index]!.ink)}
                      saved={extras[index]!.ink}
                      error={errors[`extras.${index}.ink`]}
                      onChange={(value) =>
                        setColour(`extras.${index}.ink`, value, (hex) =>
                          setExtra(index, 'ink', hex),
                        )
                      }
                    />
                    <button type="button" className="btn-ghost" onClick={() => removeExtra(index)}>
                      Remove {extra.name}
                    </button>
                  </div>
                  {ratio < EXTRA_INK_NEEDED && (
                    <p className="flex flex-wrap items-center gap-2 text-meta text-bad">
                      <span>
                        {extra.name} ink is {ratioText(ratio)} on its colour and needs{' '}
                        {EXTRA_INK_NEEDED.toFixed(1)}:1.
                      </span>
                      {fix !== null && (
                        <button
                          type="button"
                          className="btn-ghost"
                          aria-label={`Use ${fix.toUpperCase()} for the ${extra.name} ink`}
                          onClick={() => {
                            setExtra(index, 'ink', fix);
                            setTyped((current) => ({ ...current, [`extras.${index}.ink`]: fix }));
                            focusField(`extras.${index}.ink`);
                          }}
                        >
                          Use {fix.toUpperCase()}
                        </button>
                      )}
                    </p>
                  )}
                </div>
              );
            })}
            {errors.extras !== undefined && <p className="text-meta text-bad">{errors.extras}</p>}
            <div>
              <button
                type="button"
                className="btn-ghost"
                disabled={extras.length >= MAX_EXTRAS}
                onClick={addExtra}
              >
                Add an extra colour
              </button>
              {extras.length >= MAX_EXTRAS && (
                <span className="ml-2 text-meta text-ink-muted">
                  Up to {MAX_EXTRAS} extra colours.
                </span>
              )}
            </div>
          </fieldset>
        </>
      )}

      <label className="flex items-center gap-2 text-meta">
        <input
          type="checkbox"
          checked={draft.manifest.lockPalette}
          disabled={palette === undefined}
          onChange={(event) => {
            void change({ lockPalette: event.target.checked });
          }}
        />
        Lock this palette
      </label>
      <p className="text-meta text-ink-muted">
        A locked palette stops a deck from picking another one.
      </p>
    </section>
  );
}

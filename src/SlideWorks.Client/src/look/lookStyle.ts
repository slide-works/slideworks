import type { CSSProperties } from 'react';
import type { LookView } from '../api/types';

// Custom properties aren't in CSSProperties' keys.
type CustomProperties = Record<`--sw-${string}`, string | number>;

// A family goes inside quotes in a font stack: a quote or backslash would end the string early.
const quoted = (family: string) => `'${family.replace(/['"\\]/g, '')}'`;

// The Noto faces tokens.css imports, after the template's fonts and before system-ui, so Thai, Japanese,
// Korean and Chinese text is drawn in them and Latin text is untouched (SW-883). The stack is tokens.css's
// --sw-noto-stack, which puts SC or TC first under lang="zh-Hans" or "zh-Hant" (an element above the slide sets it).
const NOTO_FALLBACKS = 'var(--sw-noto-stack)';

// The look as CSS custom properties for a slide's root element (SW-2 L10):
// every palette token as --sw-<key> (accent-2 among them), the template's type and shape, and the
// tokens tokens.css derives with var() at :root. A var() resolves where it is
// declared, so the derived ones are declared again here, on the same element
// as the palette, or they would keep :root's Cobalt values.
export function lookStyle(look: LookView): CSSProperties {
  const { type, shape } = look.template;
  const style: CustomProperties = {};
  for (const [key, value] of Object.entries(look.palette.tokens)) style[`--sw-${key}`] = value;
  // A template's own mono font (the first mono file's family); with none, tokens.css's stack stands.
  const mono = look.fonts.find((font) => font.role === 'mono');
  if (mono !== undefined)
    style['--sw-font-mono'] = `${quoted(mono.family)}, ui-monospace, monospace`;
  // An extra colour is keyed by its own name (extra-1 to extra-4), with its ink beside it. The
  // resolved palette is the source of truth (a deck may override a token): an extra fills only a
  // token the palette does not already have.
  const fill = (token: `--sw-${string}`, value: string) => {
    if (!(token in style)) style[token] = value;
  };
  for (const extra of look.extras) {
    fill(`--sw-${extra.name}`, extra.colour);
    fill(`--sw-${extra.name}-ink`, extra.ink);
  }
  Object.assign(style, {
    '--sw-font-head': `'${type.head}', '${type.body}', ${NOTO_FALLBACKS}, system-ui, sans-serif`,
    '--sw-font-body': `'${type.body}', ${NOTO_FALLBACKS}, system-ui, sans-serif`,
    '--sw-head-weight': type.headWeight,
    '--sw-head-tracking': type.headTracking,
    '--sw-radius-sm': `${shape.radius.sm}px`,
    '--sw-radius-md': `${shape.radius.md}px`,
    '--sw-radius-lg': `${shape.radius.lg}px`,
    '--sw-radius-xl': `${shape.radius.xl}px`,
    '--sw-lift': shape.shadow,
    '--sw-info': 'var(--sw-accent)',
    '--sw-info-soft': 'var(--sw-accent-light)',
    '--sw-info-ink': 'var(--sw-accent-dark)',
    '--sw-night-surface': [
      'radial-gradient(1100px 700px at 88% 112%, color-mix(in srgb, var(--sw-night-glow) 45%, transparent), transparent 62%)',
      'radial-gradient(900px 600px at -5% -10%, color-mix(in srgb, var(--sw-night-glow) 12%, transparent), transparent 60%)',
      'var(--sw-night)',
    ].join(', '),
  } satisfies CustomProperties);
  return style as CSSProperties;
}

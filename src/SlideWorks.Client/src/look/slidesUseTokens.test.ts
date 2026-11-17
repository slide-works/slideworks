// @vitest-environment node
import palettesJson from '@contracts/palettes.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';

// Vitest hands CSS imports to its CSS pipeline, which empties them (even with
// ?raw), so the files are read from disk. The client has no Node types, hence
// the two calls typed here.
interface Fs {
  readFileSync(path: URL, encoding: 'utf8'): string;
  readdirSync(path: URL, options: { recursive: true; encoding: 'utf8' }): string[];
}
const fs = (
  globalThis as unknown as { process: { getBuiltinModule(id: 'node:fs'): Fs } }
).process.getBuiltinModule('node:fs');

const srcDir = new URL('../', import.meta.url);
const tokensCss = fs.readFileSync(new URL('../theme/tokens.css', import.meta.url), 'utf8');

// The folders drawn on a slide: the slides themselves, and the layers the big
// screen draws over them (SW-9's question card and reactions).
const SCANNED = ['slides/', 'qa/screen/'];

// The guard rail of L10: a slide, layout, block or chrome component takes every
// colour and font from a var(--sw-…) token, so a deck's template and palette
// re-skin it. The tests and their fixtures may spell out the colours they
// assert, so they are not scanned. Keys are paths under src/.
const sources = Object.fromEntries(
  SCANNED.flatMap((folder) =>
    fs
      .readdirSync(new URL(folder, srcDir), { recursive: true, encoding: 'utf8' })
      .map((file) => folder + file.replaceAll('\\', '/'))
      .filter((file) => /\.(ts|tsx|css)$/.test(file))
      .filter((file) => !/\.(spec|test|fixtures)\.[a-z]+$/.test(file))
      .map((file) => [file, fs.readFileSync(new URL(file, srcDir), 'utf8')]),
  ),
);

const COLOUR = [
  { name: 'a hex colour', pattern: /#[0-9a-fA-F]{3,8}\b/g },
  { name: 'an rgb() colour', pattern: /\brgba?\(/g },
  { name: 'an hsl() colour', pattern: /\bhsla?\(/g },
];

// A font family is a literal unless it is one of the --sw-font-* tokens: in a
// font-family declaration, in the font shorthand, or in a fontFamily prop.
const FONT = [
  { name: 'a font-family literal', pattern: /\bfont-family\s*:(?!\s*var\(--sw-font-)[^;}]*/g },
  {
    name: 'a font shorthand without a font token',
    pattern: /\bfont\s*:(?![^;}]*var\(--sw-font-)[^;}]*/g,
  },
  {
    name: 'a fontFamily literal',
    pattern: /\bfontFamily\s*[:=](?!\s*['"`{]*\s*var\(--sw-font-)[^,;}\n]*/g,
  },
];

/** Every colour or font literal in a source file, as "line: what (text)". */
function literalsIn(source: string): string[] {
  const found: string[] = [];
  for (const { name, pattern } of [...COLOUR, ...FONT]) {
    for (const match of source.matchAll(pattern)) {
      const line = source.slice(0, match.index).split('\n').length;
      found.push(`${line}: ${name} (${match[0].trim()})`);
    }
  }
  return found;
}

const paletteKeys = Object.keys(palettesJson.palettes.find((palette) => palette.isDefault)!.tokens);

describe('slides use tokens', () => {
  it('scans the slide sources', () => {
    expect(Object.keys(sources).length).toBeGreaterThan(20);
    expect(Object.keys(sources)).toContain('slides/layouts/GanttLayout.module.css');
    expect(Object.keys(sources)).toContain('qa/screen/QuestionCardLayer.tsx');
  });

  it('has no colour or font literal in a slide, layout, block, chrome or screen-layer file', () => {
    const literals = Object.entries(sources).flatMap(([file, source]) =>
      literalsIn(source).map((literal) => `${file}:${literal}`),
    );
    expect(literals).toEqual([]);
  });

  it('declares every palette token in tokens.css', () => {
    const missing = paletteKeys.filter((key) => !new RegExp(`--sw-${key}\\s*:`).test(tokensCss));
    expect(paletteKeys).toHaveLength(41);
    expect(missing).toEqual([]);
  });

  it('gives every palette token its default palette value in tokens.css', () => {
    const defaults = palettesJson.palettes.find((palette) => palette.isDefault)!.tokens;
    const declared = Object.fromEntries(
      [...tokensCss.matchAll(/--sw-([a-z0-9-]+)\s*:\s*([^;]+);/g)].map(([, key, value]) => [
        key,
        value.trim(),
      ]),
    );
    const differ = Object.entries(defaults)
      .filter(([key, value]) => declared[key] !== value)
      .map(([key, value]) => `${key}: tokens.css ${declared[key]}, palette ${value}`);
    expect(differ).toEqual([]);
  });

  it.each([
    ['a hex colour in CSS', '.bar {\n  color: #fff;\n}'],
    ['a long hex colour in TSX', "const style = { color: '#2f5bea' };"],
    ['an rgb() colour', '.x { background: rgb(47 91 234 / 0.45); }'],
    ['an rgba() colour', "const shade = 'rgba(0, 0, 0, 0.5)';"],
    ['an hsl() colour', '.x { color: hsl(220 80% 50%); }'],
    ['a colour in a comment', '/* the mockup used #c3d0ff */'],
    ['a font-family literal', ".x { font-family: 'Inter', sans-serif; }"],
    ['a font shorthand with a literal family', '.x { font: 700 20px/1 Inter, sans-serif; }'],
    ['a fontFamily prop', '<text fontFamily="Inter">1</text>'],
    ['a fontFamily style', "const style = { fontFamily: 'Inter' };"],
  ])('fails on %s', (_form, source) => {
    expect(literalsIn(source)).not.toEqual([]);
  });

  it.each([
    ['a colour token', '.x { color: var(--sw-on-accent); }'],
    [
      'a colour mixed from tokens',
      '.x { color: color-mix(in srgb, var(--sw-warn) 75%, var(--sw-card)); }',
    ],
    ['a font token', '.x { font-family: var(--sw-font-body); }'],
    ['a font shorthand with a font token', '.x { font: 800 64px/1.08 var(--sw-font-head); }'],
    ['a fontFamily token', "const style = { fontFamily: 'var(--sw-font-head)' };"],
  ])('accepts %s', (_form, source) => {
    expect(literalsIn(source)).toEqual([]);
  });
});

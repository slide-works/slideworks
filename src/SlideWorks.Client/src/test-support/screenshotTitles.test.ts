import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// CT runs pass --grep-invert screenshot, so a test that calls toHaveScreenshot without "screenshot" in its
// title would run, and write or compare a baseline, in the runs that are meant to skip them.

// Specs whose test keeps a title without "screenshot" because renaming it would rename its baseline PNG.
// Each entry is "<path under src>::<test title>". Add nothing new here: name a new screenshot test properly.
const KEPT_BECAUSE_OF_BASELINE: readonly string[] = [];

const srcDir = join(__dirname, '..');

function specFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      return entry.name === 'node_modules' ? [] : specFiles(path);
    }
    return entry.name.endsWith('.spec.tsx') ? [path] : [];
  });
}

// A test's text runs from its test(...) call to the first closing "})" at the same indentation, so a helper
// declared after it (one that calls toHaveScreenshot for several tests) is not charged to the test above it.
// A helper that wraps toHaveScreenshot counts too: a test that calls it takes a screenshot.
export function screenshotTestsWithoutTheWord(source: string): string[] {
  const helpers = [...source.matchAll(/^(?:async )?function (\w+)[\s\S]*?\n\}/gm)]
    .filter((helper) => helper[0].includes('toHaveScreenshot'))
    .map((helper) => helper[1]);
  const takesScreenshot = (body: string) =>
    body.includes('toHaveScreenshot') || helpers.some((name) => body.includes(`${name}(`));
  // test( and it( anywhere on a line (inside a loop or a describe block), with the usual modifiers. test.describe
  // is not a test, so its title is not checked.
  const starts = [
    ...source.matchAll(
      /(?<![\w$.])(?:test|it)(?:\.(?:only|skip|fixme|fail|slow))?\(\s*(['"`])((?:\\.|(?!\1).)*)\1/g,
    ),
  ];
  return starts.flatMap((match, index) => {
    const lineStart = source.lastIndexOf('\n', match.index) + 1;
    const indent = /^[ \t]*/.exec(source.slice(lineStart))![0];
    const rest = source.slice(match.index, starts[index + 1]?.index);
    const closing = rest.indexOf(`\n${indent}})`);
    const body = closing === -1 ? rest : rest.slice(0, closing);
    const title = match[2];
    return takesScreenshot(body) && !/screenshot/i.test(title) ? [title] : [];
  });
}

describe('screenshotTestsWithoutTheWord', () => {
  it('names a test that calls toHaveScreenshot without "screenshot" in its title', () => {
    const source =
      "test('renders a bar', async () => { await expect(c).toHaveScreenshot('a.png'); });";
    expect(screenshotTestsWithoutTheWord(source)).toEqual(['renders a bar']);
  });

  it('names a test that takes its screenshot through a helper', () => {
    const source = [
      'async function shot(page, name) {',
      '  await expect(page).toHaveScreenshot(name);',
      '}',
      "test('follows the look', async ({ page }) => {",
      "  await shot(page, 'a.png');",
      '});',
    ].join('\n');
    expect(screenshotTestsWithoutTheWord(source)).toEqual(['follows the look']);
  });

  it('accepts the word in any case and ignores tests without a screenshot', () => {
    const source = [
      "test('Screenshot: a bar', async () => { await expect(c).toHaveScreenshot('a.png'); });",
      "test('renders text', async () => { await expect(c).toBeVisible(); });",
    ].join('\n');
    expect(screenshotTestsWithoutTheWord(source)).toEqual([]);
  });

  it('names a bad title in an it( test', () => {
    const source = "it('renders a bar', async () => { await expect(c).toHaveScreenshot('a.png'); });";
    expect(screenshotTestsWithoutTheWord(source)).toEqual(['renders a bar']);
  });

  it('names a bad title in a test( that is not at the start of a line', () => {
    const source = [
      'for (const name of names) {',
      "  if (name) test('renders ' + name, async () => {",
      "    await expect(c).toHaveScreenshot('a.png');",
      '  });',
      '}',
      "describe('x', () => { test('inline bar', async () => { await expect(c).toHaveScreenshot('b.png'); }); });",
    ].join('\n');
    expect(screenshotTestsWithoutTheWord(source)).toEqual(['renders ', 'inline bar']);
  });

  it('names a bad title in an it( inside a describe block', () => {
    const source = [
      "test.describe('bars', () => {",
      "  it('renders a bar', async () => {",
      "    await expect(c).toHaveScreenshot('a.png');",
      '  });',
      '});',
    ].join('\n');
    expect(screenshotTestsWithoutTheWord(source)).toEqual(['renders a bar']);
  });

  it('does not count a test.describe title as a test', () => {
    const source = [
      "test.describe('bars', () => {",
      "  test('bar matches its screenshot', async () => {",
      "    await expect(c).toHaveScreenshot('a.png');",
      '  });',
      '});',
    ].join('\n');
    expect(screenshotTestsWithoutTheWord(source)).toEqual([]);
  });
});

describe('screenshot test titles', () => {
  it('say "screenshot" in every spec test that calls toHaveScreenshot', () => {
    const offenders = specFiles(srcDir).flatMap((file) => {
      const relative = file
        .slice(srcDir.length + 1)
        .split('\\')
        .join('/');
      return screenshotTestsWithoutTheWord(readFileSync(file, 'utf8'))
        .map((title) => `${relative}::${title}`)
        .filter((entry) => !KEPT_BECAUSE_OF_BASELINE.includes(entry));
    });
    expect(
      offenders,
      'rename the test so its title has "screenshot", or allow-list it with the reason',
    ).toEqual([]);
  });
});

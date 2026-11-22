// @vitest-environment node
import { describe, expect, it } from 'vitest';

// The client has no Node types, hence the calls typed here (as in look/slidesUseTokens.test.ts).
interface Fs {
  readFileSync(path: URL, encoding: 'utf8'): string;
  readdirSync(path: URL, options: { recursive: true; encoding: 'utf8' }): string[];
}
const fs = (
  globalThis as unknown as { process: { getBuiltinModule(id: 'node:fs'): Fs } }
).process.getBuiltinModule('node:fs');

const clientSrc = new URL('../', import.meta.url);
const pagesDir = new URL('../../../SlideWorks.AcceptanceTests/Pages/', import.meta.url);

// A test id that no acceptance page object uses yet, with the reason it need not be. A new id is not added here to
// make this test pass: it gets a member in a page object under src/SlideWorks.AcceptanceTests/Pages. This list was
// seeded with the ids unused on the day the guard was added; shrink it as page objects grow. Keys are the id as it is
// written in testIds.ts (a template id is cut at its first ${).
const UNUSED_TEST_IDS: Record<string, string> = {
  'open-response-panel':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-strip':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-phase':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-guests':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-open':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-close':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-new-round':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-control-error':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-entry-':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-hide-':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-restore-':
    'SW-8 presenter open-response controls; no acceptance journey drives them yet',
  'open-response-word-card':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-answer-card':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-question':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-input':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-submit':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-mine':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-mine-entry':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-room':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-room-entry':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'open-response-error':
    'SW-8 phone word-cloud and open-text cards; no acceptance journey reads them yet',
  'qa-card-dim': 'drawn on the big screen only; no acceptance page object reads it yet',
  'templates-error': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-public-page': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-error': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-save-error':
    'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-tabs': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-panel': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-preview':
    'Templates area; the acceptance suite has no Templates page object yet',
  'template-publish-checklist':
    'Templates area; the acceptance suite has no Templates page object yet',
  'layout-review-gradient-note': 'covered by component tests only; no acceptance journey reads it',
  'layout-review-error': 'covered by component tests only; no acceptance journey reads it',
  'template-short-name': 'covered by component tests only; no acceptance journey reads it',
  'font-to-add-role': 'covered by component tests only; no acceptance journey reads it',
};

interface TestId {
  file: string;
  id: string;
}

const testIdFiles = fs
  .readdirSync(clientSrc, { recursive: true, encoding: 'utf8' })
  .filter((path) => path.endsWith('/testIds.ts') || path === 'testIds.ts')
  .sort();

interface ParsedId {
  id: string;
  // A template id (`id-${n}`) is cut at its first ${, so it is a prefix; any other id is a whole string.
  isTemplate: boolean;
}

// Every id in the text of a testIds file: the value of an entry (`key: 'id'` or `key: "id"`, optionally followed by a
// comment) and the text of a template id (`id-${n}` counts up to its first ${). Imports, comments and the arrow
// functions around template ids are not entries. An indented `key: value` line that is not a quoted id throws, naming
// the file and line, so an entry this parser cannot read is never skipped.
function parseIds(text: string, file: string): ParsedId[] {
  const ids: ParsedId[] = [];
  text.split('\n').forEach((line, index) => {
    if (!/^\s+\w+:/.test(line)) return;
    // An entry that is an arrow function over a template id is read by the template pass below.
    if (/^\s+\w+:\s*\([^)]*\)\s*=>/.test(line)) return;
    const entry = /^\s+\w+:\s*(?:'([^']+)'|"([^"]+)"),?\s*(?:\/\/.*)?$/.exec(line);
    if (entry === null) {
      throw new Error(`${file}:${index + 1}: cannot read this testIds entry: ${line.trim()}`);
    }
    ids.push({ id: entry[1] ?? entry[2], isTemplate: false });
  });
  for (const match of text.matchAll(/=>\s*`([^`]+)`/g)) {
    ids.push({ id: match[1].split('${')[0], isTemplate: true });
  }
  return ids;
}

// A literal id is used when a page object has it as a whole quoted string ("id" or 'id'); only a template id may be
// matched as the start of a longer quoted string.
function isUsedIn(pagesText: string, { id, isTemplate }: ParsedId): boolean {
  if (pagesText.includes(`"${id}"`) || pagesText.includes(`'${id}'`)) return true;
  return isTemplate && (pagesText.includes(`"${id}`) || pagesText.includes(`'${id}`));
}

const pages = fs
  .readdirSync(pagesDir, { recursive: true, encoding: 'utf8' })
  .filter((path) => path.endsWith('.cs'))
  .map((path) => fs.readFileSync(new URL(path, pagesDir), 'utf8'))
  .join('\n');

const allIds: (TestId & ParsedId)[] = testIdFiles.flatMap((file) =>
  parseIds(fs.readFileSync(new URL(file, clientSrc), 'utf8'), file).map((parsed) => ({
    file,
    ...parsed,
  })),
);
const isUsed = (entry: ParsedId) => isUsedIn(pages, entry);

describe('parseIds and isUsedIn', () => {
  it('reads single-quoted, double-quoted and commented entries', () => {
    const text = [
      'export const IDS = {',
      "  single: 'a-single',",
      '  double: "a-double",',
      "  commented: 'a-commented', // why",
      '  doubleCommented: "a-double-commented" // why',
      "  last: 'a-last'",
      '} as const;',
    ].join('\n');

    expect(parseIds(text, 'x/testIds.ts').map(({ id }) => id)).toEqual([
      'a-single',
      'a-double',
      'a-commented',
      'a-double-commented',
      'a-last',
    ]);
  });

  it('reads a template id up to its first ${ and marks it as a prefix', () => {
    const text = 'export const rowTestId = (n: number) => `row-${n}-x`;';

    expect(parseIds(text, 'x/testIds.ts')).toEqual([{ id: 'row-', isTemplate: true }]);
  });

  it('fails loudly, naming the file and line, on an entry it cannot read', () => {
    const text = ['export const IDS = {', "  ok: 'fine',", '  bad: `tick`,', '};'].join('\n');

    expect(() => parseIds(text, 'x/testIds.ts')).toThrow(/x\/testIds\.ts:3: cannot read/);
  });

  it('does not count a longer id as a use of a literal id', () => {
    const pagesText = 'By.TestId("qa-tab-slide")';

    expect(isUsedIn(pagesText, { id: 'qa-tab', isTemplate: false })).toBe(false);
    expect(isUsedIn(pagesText, { id: 'qa-tab-slide', isTemplate: false })).toBe(true);
  });

  it('counts a whole quoted string in either quote style as a use', () => {
    expect(isUsedIn("Id('a')", { id: 'a', isTemplate: false })).toBe(true);
    expect(isUsedIn('Id("a")', { id: 'a', isTemplate: false })).toBe(true);
  });

  it('lets a template id match the start of a longer quoted string', () => {
    expect(isUsedIn('Id($"qa-tab-{n}")', { id: 'qa-tab-', isTemplate: true })).toBe(true);
    expect(isUsedIn('Id("other")', { id: 'qa-tab-', isTemplate: true })).toBe(false);
  });
});

describe('test ids and the acceptance page objects', () => {
  it('finds the testIds files and their ids', () => {
    expect(testIdFiles.length).toBeGreaterThanOrEqual(6);
    expect(allIds.length).toBeGreaterThan(200);
  });

  it('uses every test id in a page object, or lists it in UNUSED_TEST_IDS with a reason', () => {
    const missing = allIds
      .filter((entry) => !isUsed(entry) && !(entry.id in UNUSED_TEST_IDS))
      .map(({ file, id }) => `${id} (${file})`);

    expect(
      missing,
      'These test ids have no use in src/SlideWorks.AcceptanceTests/Pages. Add a member for each to a page object, or put it in UNUSED_TEST_IDS in this file with a reason:',
    ).toEqual([]);
  });

  it('keeps no entry in UNUSED_TEST_IDS that a page object now uses or that no testIds file has', () => {
    const stale = Object.keys(UNUSED_TEST_IDS).filter((id) => {
      const entry = allIds.find((candidate) => candidate.id === id);
      return entry === undefined || isUsed(entry);
    });

    expect(stale, 'Remove these from UNUSED_TEST_IDS in this file:').toEqual([]);
  });
});

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
  'templates-page': 'Templates area; the acceptance suite has no Templates page object yet',
  'templates-error': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-row': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-name': 'Templates area; the acceptance suite has no Templates page object yet',
  'new-template-dialog': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-share-dialog': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-public-page': 'Templates area; the acceptance suite has no Templates page object yet',
  'layout-review-page': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-error': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-save-error':
    'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-tabs': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-panel': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-preview':
    'Templates area; the acceptance suite has no Templates page object yet',
  'template-publish-checklist':
    'Templates area; the acceptance suite has no Templates page object yet',
  'template-editor-tab-': 'Templates area; the acceptance suite has no Templates page object yet',
  'template-tab-': 'Templates area; the acceptance suite has no Templates page object yet',
};

interface TestId {
  file: string;
  id: string;
}

const testIdFiles = fs
  .readdirSync(clientSrc, { recursive: true, encoding: 'utf8' })
  .filter((path) => path.endsWith('/testIds.ts') || path === 'testIds.ts')
  .sort();

// Every quoted id in a testIds file: the value of an entry (`key: 'id'`) and the text of a template id (`id-${n}`
// counts up to its first ${). Imports, comments and other strings are not ids.
function idsIn(file: string): string[] {
  const text = fs.readFileSync(new URL(file, clientSrc), 'utf8');
  const ids: string[] = [];
  for (const match of text.matchAll(/^\s+\w+:\s*'([^']+)',?$/gm)) ids.push(match[1]);
  for (const match of text.matchAll(/=>\s*`([^`]+)`/g)) ids.push(match[1].split('${')[0]);
  return ids;
}

const pages = fs
  .readdirSync(pagesDir, { recursive: true, encoding: 'utf8' })
  .filter((path) => path.endsWith('.cs'))
  .map((path) => fs.readFileSync(new URL(path, pagesDir), 'utf8'))
  .join('\n');

const allIds: TestId[] = testIdFiles.flatMap((file) => idsIn(file).map((id) => ({ file, id })));
const isUsed = ({ id }: TestId) =>
  pages.includes(`"${id}"`) || (!id.endsWith('"') && pages.includes(`"${id}`));

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

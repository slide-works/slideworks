import { expect, test } from 'vitest';
import { deckChecksSample } from './deckChecks';

test('the shared sample parses as DeckChecksView', () => {
  expect(Object.keys(deckChecksSample).sort()).toEqual([
    'deck',
    'error',
    'findings',
    'languages',
    'measured',
    'slides',
  ]);
  expect(deckChecksSample.languages).toEqual([]);
  expect(deckChecksSample.error).toBeNull();
  expect(deckChecksSample.findings.map((f) => [f.slide, f.rule, f.severity, f.source])).toEqual([
    [1, 'fit', 'error', 'stored'],
    [2, 'overflow', 'error', 'measured'],
    [3, 'textOnImage', 'note', 'measured'],
  ]);
  for (const finding of deckChecksSample.findings) {
    expect(finding.language).toBeNull();
    expect(Object.keys(finding).sort()).toEqual([
      'language',
      'message',
      'rule',
      'severity',
      'slide',
      'source',
      'where',
    ]);
  }
});

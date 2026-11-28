import { readFileSync } from 'node:fs';
import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

const appDir = decodeURIComponent(new URL('..', import.meta.url).pathname);
const fixture = readFileSync(new URL('../fixtures/importsTestSupport.ts', import.meta.url), 'utf8');

async function restrictedImportLines(filePath: string) {
  const [result] = await new ESLint({ cwd: appDir }).lintText(fixture, {
    filePath: `${appDir}${filePath}`,
  });
  return result.messages.filter((m) => m.ruleId === 'no-restricted-imports').map((m) => m.line);
}

describe('lint', () => {
  it('only tests import test-support', async () => {
    expect(await restrictedImportLines('src/screen.ts')).toEqual([1, 2]);
    expect(await restrictedImportLines('app/talks.tsx')).toEqual([1, 2]);
    expect(await restrictedImportLines('src/screen.test.ts')).toEqual([]);
    expect(await restrictedImportLines('src/screen.spec.tsx')).toEqual([]);
    expect(await restrictedImportLines('src/test-support/renderScreen.ts')).toEqual([]);
  });
});

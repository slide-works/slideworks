import { readFileSync } from 'node:fs';
import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';
import { corePackageName } from '@slideworks/core/index';

const coreDir = decodeURIComponent(new URL('..', import.meta.url).pathname);

describe('core', () => {
  it('lint bans browser globals and UI imports', async () => {
    const code = readFileSync(new URL('../fixtures/violations.ts', import.meta.url), 'utf8');
    const eslint = new ESLint({ cwd: coreDir });

    // Linted as a file under src/, where the core's own code lives.
    const [result] = await eslint.lintText(code, { filePath: `${coreDir}src/violations.ts` });
    const banned = result.messages
      .filter(
        (m) =>
          m.ruleId === 'no-restricted-globals' ||
          m.ruleId === 'no-restricted-properties' ||
          m.ruleId === 'no-restricted-imports',
      )
      .map((m) => `${m.ruleId}:${m.line}`);

    expect(banned).toEqual([
      'no-restricted-imports:2',
      'no-restricted-imports:3',
      'no-restricted-imports:4',
      'no-restricted-imports:5',
      'no-restricted-globals:8',
      'no-restricted-globals:9',
      'no-restricted-globals:10',
      'no-restricted-globals:11',
      'no-restricted-globals:12',
      'no-restricted-properties:13',
      'no-restricted-properties:14',
      'no-restricted-properties:15',
      'no-restricted-properties:16',
      'no-restricted-properties:17',
      'no-restricted-properties:18',
      'no-restricted-properties:19',
      'no-restricted-properties:20',
      'no-restricted-properties:21',
      'no-restricted-properties:22',
    ]);
  });

  it('package exports resolve', () => {
    expect(corePackageName).toBe('@slideworks/core');
  });
});

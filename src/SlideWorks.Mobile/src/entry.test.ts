import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

describe('entry', () => {
  it('crash reports load before polyfills and the router', () => {
    const entry = readFileSync(new URL('../index.ts', import.meta.url), 'utf8');
    // Every import statement, bare or named, in source order.
    const imports = [
      ...entry.matchAll(/^import\s+(?:[^'"]*?\s+from\s+)?['"]([^'"]+)['"];?$/gm),
    ].map((m) => m[1]);

    expect(imports).toEqual(['./src/crashReports', './src/polyfills', 'expo-router/entry']);
  });
});

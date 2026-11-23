// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { glyphCost, glyphFactor } from './glyphWidths';

// Vitest has no fs import here (see custom/allowTokens.test.ts), so the server file is read from disk.
interface Fs {
  readFileSync(path: URL, encoding: 'utf8'): string;
}
const fs = (
  globalThis as unknown as { process: { getBuiltinModule(id: 'node:fs'): Fs } }
).process.getBuiltinModule('node:fs');

const server = fs.readFileSync(
  new URL('../../../../SlideWorks.Core/Slides/GlyphWidths.cs', import.meta.url),
  'utf8',
);

// The server's factor for a character, read from GlyphWidths.cs: its constants and the switch arms.
function serverFactors(): (c: string) => number {
  const constants = new Map(
    [...server.matchAll(/private const double (\w+) = ([\d.]+);/g)].map((m) => [m[1], Number(m[2])]),
  );
  const arms: { test: (c: string) => boolean; value: number }[] = [];
  for (const m of server.matchAll(/^\s+(.+?) => (\w+),$/gm)) {
    const value = constants.get(m[2]);
    if (value === undefined) continue;
    const pattern = m[1];
    const singles = [...pattern.matchAll(/'(.)'/g)].map((x) => x[1]);
    const range = pattern.match(/>= '(.)' and <= '(.)'/);
    arms.push({
      test: (c) => singles.includes(c) || (range !== null && c >= range[1] && c <= range[2]),
      value,
    });
  }
  return (c) => arms.find((a) => a.test(c))?.value ?? 1;
}

describe('glyphWidths', () => {
  it('matches the server GlyphWidths table for every printable ASCII character', () => {
    const factor = serverFactors();
    for (let code = 32; code < 127; code++) {
      const c = String.fromCharCode(code);
      expect(glyphFactor(c), `factor of ${c}`).toBe(factor(c));
    }
    expect(glyphFactor('é')).toBe(1);
  });

  it('never charges a character below 1', () => {
    for (let code = 32; code < 400; code++) {
      expect(glyphFactor(String.fromCharCode(code))).toBeGreaterThanOrEqual(1);
    }
  });

  it('sums the factors of a text', () => {
    expect(glyphCost('Wa1')).toBeCloseTo(1.87 + 1 + 1.22, 10);
    expect(glyphCost('é')).toBe(1);
  });
});

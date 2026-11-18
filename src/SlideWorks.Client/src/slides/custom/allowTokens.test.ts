// @vitest-environment node
import allowJson from '@contracts/custom-layout-allow.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';

// Vitest empties CSS imports, so tokens.css is read from disk (as slidesUseTokens.test.ts does).
interface Fs {
  readFileSync(path: URL, encoding: 'utf8'): string;
}
const fs = (
  globalThis as unknown as { process: { getBuiltinModule(id: 'node:fs'): Fs } }
).process.getBuiltinModule('node:fs');

const tokensCss = fs.readFileSync(new URL('../../theme/tokens.css', import.meta.url), 'utf8');
const declared = new Set([...tokensCss.matchAll(/(--sw-[a-z0-9-]+)\s*:/g)].map((m) => m[1]));
const allowed = [...allowJson.tokens, ...allowJson.fontTokens];

// The tokens a custom layout's CSS may read (src/contracts/custom-layout-allow.json) against the
// ones the app declares. A token missing from the allow file is not an error: a layout simply
// cannot use it.
describe('the custom-layout allow file’s tokens', () => {
  it('are all declared in tokens.css, except the extras a template sets', () => {
    expect(allowed.length).toBeGreaterThan(0);
    expect(declared.size).toBeGreaterThan(0);
    const missing = allowed.filter(
      (name) => !name.startsWith('--sw-extra-') && !declared.has(name),
    );
    expect(missing).toEqual([]);
  });

  // A template fills --sw-lift from text, and a layout must never read a free-text value.
  it('holds no free-text token', () => {
    expect(allowed).not.toContain('--sw-lift');
    expect(allowed).not.toContain('--sw-night-surface');
  });
});

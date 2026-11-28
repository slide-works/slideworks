import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { generatedPath, generateTokens, tokensCssPath } from '../../scripts/generate-tokens.mjs';

describe('tokens', () => {
  it('generated file is current', () => {
    const expected = generateTokens(readFileSync(tokensCssPath, 'utf8'));

    // Stale: run `pnpm --dir src/SlideWorks.Mobile run tokens` and commit the result.
    expect(readFileSync(generatedPath, 'utf8')).toBe(expected);
  });
});

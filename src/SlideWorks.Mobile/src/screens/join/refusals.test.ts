import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { JOIN_REFUSALS, NO_SESSION_LEAD, REFUSAL_ADVICE, spacedCode } from './refusals';

// The web's join keypad, whose words the app's refusals copy (K7).
const webSource = readFileSync(
  resolve(__dirname, '../../../../SlideWorks.Client/src/guest/JoinCodePage.tsx'),
  'utf8',
);

describe('join refusals', () => {
  it("say the web's words", () => {
    for (const refusal of JOIN_REFUSALS) {
      expect(webSource).toContain(`${refusal}: '${REFUSAL_ADVICE[refusal]}'`);
    }
    expect(webSource).toContain(`${NO_SESSION_LEAD}<b>`);
  });

  it('space the code like the web', () => {
    expect(spacedCode('123456')).toBe('123 456');
    expect(webSource).toContain('`${code.slice(0, 3)} ${code.slice(3)}`');
  });
});

// @vitest-environment node
import { ESLint } from 'eslint';
import { beforeAll, describe, expect, it } from 'vitest';

// One ESLint for every case. Its first lint loads the config, the TypeScript
// parser and the plugins, which takes seconds on a loaded host, so beforeAll
// pays for that once and each case gets a generous timeout of its own.
const TIMEOUT_MS = 30_000;
let eslint: ESLint;

async function lint(lines: string[]) {
  const [result] = await eslint.lintText(lines.join('\n') + '\n', {
    filePath: 'src/Snippet.tsx',
  });
  return result.messages;
}

// The guard rail in eslint.config.js: slot text is plain text, so every way of
// injecting HTML fails the lint.
describe('lint guard', { timeout: TIMEOUT_MS }, () => {
  beforeAll(async () => {
    eslint = new ESLint();
    await lint(['export const warm = 1;']);
  }, TIMEOUT_MS);

  it.each([
    [
      'the JSX attribute',
      ['export const A = (t: string) => <p dangerouslySetInnerHTML={{ __html: t }} />;'],
    ],
    [
      'a spread props object',
      [
        'export function A(t: string) {',
        '  const props = { dangerouslySetInnerHTML: { __html: t } };',
        '  return <p {...props} />;',
        '}',
      ],
    ],
    [
      'React.createElement',
      [
        "import { createElement } from 'react';",
        "export const A = (t: string) => createElement('p', { 'dangerouslySetInnerHTML': { __html: t } });",
      ],
    ],
    [
      'an innerHTML assignment',
      ['export function A(el: HTMLElement, t: string) {', '  el.innerHTML = t;', '}'],
    ],
    [
      'an outerHTML assignment',
      ['export function A(el: HTMLElement, t: string) {', '  el.outerHTML = t;', '}'],
    ],
    [
      'insertAdjacentHTML',
      [
        'export function A(el: HTMLElement, t: string) {',
        "  el.insertAdjacentHTML('beforeend', t);",
        '}',
      ],
    ],
  ])('rejects %s', async (_form, lines) => {
    const messages = await lint(lines);
    expect(messages.map((message) => message.ruleId)).toContain('no-restricted-syntax');
  });

  it('accepts slot text as a plain text node', async () => {
    const messages = await lint([
      'export function Safe({ text }: { text: string }) {',
      '  return <p>{text}</p>;',
      '}',
    ]);
    expect(messages).toEqual([]);
  });
});

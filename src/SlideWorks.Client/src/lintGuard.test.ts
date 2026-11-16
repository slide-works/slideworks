// @vitest-environment node
import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

// The guard rail in eslint.config.js: slot text is plain text, so every way of
// injecting HTML fails the lint.
async function lint(lines: string[]) {
  const [result] = await new ESLint().lintText(lines.join('\n') + '\n', {
    filePath: 'src/Snippet.tsx',
  });
  return result.messages;
}

describe('lint guard', () => {
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

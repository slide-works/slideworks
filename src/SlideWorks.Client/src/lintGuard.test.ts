// @vitest-environment node
import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

// The guard rail in eslint.config.js: slot text is plain text, so injecting
// HTML through dangerouslySetInnerHTML fails the lint.
describe('lint guard', () => {
  it('rejects dangerouslySetInnerHTML', async () => {
    const snippet = [
      'export function Unsafe({ text }: { text: string }) {',
      '  return <p dangerouslySetInnerHTML={{ __html: text }} />;',
      '}',
      '',
    ].join('\n');

    const [result] = await new ESLint().lintText(snippet, { filePath: 'src/Unsafe.tsx' });

    expect(result.messages.map((message) => message.ruleId)).toContain('no-restricted-syntax');
    expect(result.errorCount).toBeGreaterThan(0);
  });

  it('accepts slot text as a plain text node', async () => {
    const snippet = [
      'export function Safe({ text }: { text: string }) {',
      '  return <p>{text}</p>;',
      '}',
      '',
    ].join('\n');

    const [result] = await new ESLint().lintText(snippet, { filePath: 'src/Safe.tsx' });

    expect(result.messages).toEqual([]);
  });
});

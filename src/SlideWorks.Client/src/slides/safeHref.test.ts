import linkCases from '@contracts/link-cases.json';
import { describe, expect, it } from 'vitest';
import { safeHref } from './safeHref';

const cases: { value: string; ok: boolean }[] = linkCases.cases;

describe('safeHref', () => {
  it('has every shared case', () => {
    expect(cases.length).toBeGreaterThanOrEqual(75);
    expect(cases.some((c) => c.ok)).toBe(true);
    expect(cases.some((c) => !c.ok)).toBe(true);
  });

  it.each(cases.map((c, i) => [i, JSON.stringify(c.value).slice(0, 60), c] as const))(
    'matches shared case %i: %s',
    (_, __, { value, ok }) => {
      // An accepted link is the text as given, never trimmed or rewritten.
      expect(safeHref(value)).toBe(ok ? value : undefined);
    },
  );

  it('refuses javascript:', () => {
    expect(safeHref('javascript:alert(1)')).toBeUndefined();
    expect(safeHref('JaVaScRiPt:alert(1)')).toBeUndefined();
    expect(safeHref(' javascript:alert(1)')).toBeUndefined();
    expect(safeHref('java\tscript:alert(1)')).toBeUndefined();
    expect(safeHref('javascript://example.org/%0Aalert(1)')).toBeUndefined();
  });

  it('refuses a value that is not text', () => {
    expect(safeHref(undefined)).toBeUndefined();
    expect(safeHref(null as unknown as string)).toBeUndefined();
    expect(safeHref(['https://example.org'] as unknown as string)).toBeUndefined();
    expect(
      safeHref({ toString: () => 'https://example.org' } as unknown as string),
    ).toBeUndefined();
  });

  it('checks the length first: 500 characters pass and 501 do not', () => {
    const at = (length: number) => 'https://example.org/'.padEnd(length, 'a');
    expect(safeHref(at(500))).toBe(at(500));
    expect(safeHref(at(501))).toBeUndefined();
  });

  it('refuses every character outside printable ASCII, wherever it sits', () => {
    const outside = [0x00, 0x09, 0x0a, 0x0d, 0x1f, 0x20, 0x7f, 0x80, 0x85, 0xa0, 0x2028, 0xfeff];
    for (const code of outside) {
      const c = String.fromCharCode(code);
      expect(safeHref(`${c}https://example.org/`)).toBeUndefined();
      expect(safeHref(`htt${c}ps://example.org/`)).toBeUndefined();
      expect(safeHref(`https://exa${c}mple.org/`)).toBeUndefined();
      expect(safeHref(`https://example.org/a${c}`)).toBeUndefined();
    }
    expect(safeHref('https://example.org/\u{1F600}')).toBeUndefined();
  });

  it('refuses user info and a percent sign before the path, and allows both after it', () => {
    expect(safeHref('https://a@example.org')).toBeUndefined();
    expect(safeHref('https://example.org@evil.example?x')).toBeUndefined();
    expect(safeHref('https://example.org%40evil.example#x')).toBeUndefined();
    expect(safeHref('https://example.org/@a')).toBe('https://example.org/@a');
    expect(safeHref('https://example.org?a=b@c')).toBe('https://example.org?a=b@c');
    expect(safeHref('https://example.org#a@b%20c')).toBe('https://example.org#a@b%20c');
  });

  it('refuses punctuation in the authority: only a host and a port sit before the path', () => {
    for (const value of [
      'http://example.org"onclick="alert(1)',
      "https://example.org'x/",
      'https://exa<mple.org/',
      'https://example.org(1)/',
      'https://exam_ple.org/',
      'https://example.org;x/',
      'https://.example.org/',
      'https://example.org-/',
      'https://example.org:port/',
      'https://example.org:123456/',
      'https://[::1/',
      'https://[::g]/',
    ]) {
      expect(safeHref(value)).toBeUndefined();
    }
  });

  it('refuses an address with no host', () => {
    for (const value of [
      'http://',
      'https://',
      'https:///a',
      'https://:80/',
      'https://?a',
      'https://#a',
    ]) {
      expect(safeHref(value)).toBeUndefined();
    }
  });
});

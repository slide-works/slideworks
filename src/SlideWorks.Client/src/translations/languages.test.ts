import languagesJson from '@contracts/languages.json' with { type: 'json' };
import { describe, expect, it } from 'vitest';
import { languageOf, supportedLanguages } from './languages';

describe('languages', () => {
  it('lists every language in the catalogue, in its order', () => {
    expect(supportedLanguages.map((l) => l.code)).toEqual(
      languagesJson.languages.map((l) => l.code),
    );
    expect(supportedLanguages).toHaveLength(15);
  });

  it('finds a language by its code', () => {
    expect(languageOf('th')).toEqual({
      code: 'th',
      name: 'ไทย',
      englishName: 'Thai',
      short: 'ไทย',
      script: 'thai',
    });
    expect(languageOf('zh-Hant')?.englishName).toBe('Chinese (Traditional)');
  });

  it('has no right-to-left or unknown language', () => {
    expect(languageOf('ar')).toBeUndefined();
    expect(languageOf('xx')).toBeUndefined();
  });
});

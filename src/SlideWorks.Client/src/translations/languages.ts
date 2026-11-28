import languagesJson from '@contracts/languages.json' with { type: 'json' };

// The languages a deck can be translated into (SW-883 D10), from src/contracts/languages.json, which the server's
// catalogue reads too. name is the language's own name, englishName its name in English, short the phone chip's label.
export type SupportedLanguage = {
  code: string;
  name: string;
  englishName: string;
  short: string;
  script: string;
};

export const supportedLanguages: readonly SupportedLanguage[] = languagesJson.languages;

// The supported language with this code, or undefined for a code the catalogue doesn't have.
export function languageOf(code: string): SupportedLanguage | undefined {
  return supportedLanguages.find((language) => language.code === code);
}

import type { GuestTranslations } from '../guestView';

// The language a phone starts in (SW-883 D3). fromPhone is true when the phone's own language was picked, which is the
// only case that shows the banner.
export type StartLanguage = { code: string; fromPhone: boolean };

const CHINESE_TRADITIONAL_REGIONS = new Set(['tw', 'hk', 'mo']);

// The offered code a phone language tag stands for, or null. The exact code first, then Chinese (zh-TW, zh-HK, zh-MO and
// any zh-Hant tag are Traditional, every other zh is Simplified), then the base subtag (pt-BR is pt).
export function matchLanguage(offered: readonly string[], tag: string): string | null {
  const parts = tag.toLowerCase().split('-');
  const find = (code: string) =>
    offered.find((o) => o.toLowerCase() === code.toLowerCase()) ?? null;
  const exact = find(parts.join('-'));
  if (exact) return exact;
  if (parts[0] === 'zh') {
    const traditional =
      parts.includes('hant') || parts.some((p) => CHINESE_TRADITIONAL_REGIONS.has(p));
    return find(traditional ? 'zh-Hant' : 'zh-Hans');
  }
  return find(parts[0]);
}

// A pick kept for this session, else the first of the phone's languages the deck offers, else the primary.
export function startLanguage(
  translations: GuestTranslations,
  phoneLanguages: readonly string[],
  stored: string | null,
): StartLanguage {
  const offered = translations.languages.map((language) => language.code);
  if (stored !== null && offered.includes(stored)) return { code: stored, fromPhone: false };
  for (const tag of phoneLanguages) {
    const code = matchLanguage(offered, tag);
    if (code) return { code, fromPhone: code !== translations.primary };
  }
  return { code: translations.primary, fromPhone: false };
}

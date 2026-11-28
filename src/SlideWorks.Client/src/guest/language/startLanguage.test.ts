import { describe, expect, it } from 'vitest';
import { guestTranslations } from '../../test-support/fixtures/guestStates';
import type { GuestTranslations } from '../guestView';
import { matchLanguage, startLanguage } from './startLanguage';

const deck = guestTranslations();
const chinese: GuestTranslations = {
  primary: 'en',
  languages: ['en', 'zh-Hans', 'zh-Hant', 'pt'].map((code) => ({
    code,
    name: code,
    englishName: code,
    short: code,
    complete: 100,
  })),
};
const offered = chinese.languages.map((language) => language.code);

describe('matchLanguage', () => {
  it('startLanguage_WhenZhTw_ShouldPickTraditional', () => {
    expect(matchLanguage(offered, 'zh-TW')).toBe('zh-Hant');
    expect(matchLanguage(offered, 'zh-HK')).toBe('zh-Hant');
    expect(matchLanguage(offered, 'zh-Hant-TW')).toBe('zh-Hant');
  });

  it('startLanguage_WhenOtherZh_ShouldPickSimplified', () => {
    expect(matchLanguage(offered, 'zh')).toBe('zh-Hans');
    expect(matchLanguage(offered, 'zh-CN')).toBe('zh-Hans');
    expect(matchLanguage(offered, 'zh-SG')).toBe('zh-Hans');
  });

  it('startLanguage_WhenExactCode_ShouldMatchItIgnoringCase', () => {
    expect(matchLanguage(offered, 'zh-hans')).toBe('zh-Hans');
  });

  it('startLanguage_WhenRegionTag_ShouldMatchTheBaseSubtag', () => {
    expect(matchLanguage(offered, 'pt-BR')).toBe('pt');
  });

  it('startLanguage_WhenNotOffered_ShouldMatchNothing', () => {
    expect(matchLanguage(offered, 'fr-FR')).toBeNull();
    expect(matchLanguage(['en'], 'zh-TW')).toBeNull();
  });
});

describe('startLanguage', () => {
  it('startLanguage_WhenPhoneLanguageOffered_ShouldPickItFromThePhone', () => {
    expect(startLanguage(deck, ['th-TH', 'en'], null)).toEqual({ code: 'th', fromPhone: true });
  });

  it('startLanguage_WhenFirstPhoneLanguageNotOffered_ShouldTryTheNext', () => {
    expect(startLanguage(deck, ['fr-FR', 'ja'], null)).toEqual({ code: 'ja', fromPhone: true });
  });

  it('startLanguage_WhenPhoneLanguageIsThePrimary_ShouldStayWithoutBanner', () => {
    expect(startLanguage(deck, ['en-GB', 'th'], null)).toEqual({ code: 'en', fromPhone: false });
  });

  it('startLanguage_WhenNothingMatches_ShouldPickThePrimary', () => {
    expect(startLanguage(deck, ['fr-FR'], null)).toEqual({ code: 'en', fromPhone: false });
    expect(startLanguage(deck, [], null)).toEqual({ code: 'en', fromPhone: false });
  });

  it('startLanguage_WhenPickStored_ShouldWinOverThePhone', () => {
    expect(startLanguage(deck, ['th'], 'ja')).toEqual({ code: 'ja', fromPhone: false });
  });

  it('startLanguage_WhenStoredPickNoLongerOffered_ShouldIgnoreIt', () => {
    expect(startLanguage(deck, ['th'], 'fr')).toEqual({ code: 'th', fromPhone: true });
  });
});

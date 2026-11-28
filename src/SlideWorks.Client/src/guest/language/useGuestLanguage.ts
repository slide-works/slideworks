import { useCallback, useEffect, useState } from 'react';
import type { AudienceLanguage } from '../audienceEvents';
import type { GuestTranslations } from '../guestView';
import { startLanguage, type StartLanguage } from './startLanguage';

const storageKey = (code: string) => `sw-guest-language:${code}`;
const bannerKey = (code: string) => `sw-guest-language-banner:${code}`;

function read(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string) {
  try {
    sessionStorage.setItem(key, value);
  } catch {
    // Storage is a convenience: without it the pick lasts until the page reloads.
  }
}

// The language a phone starts in for this join code: the pick kept for the session, else the phone's own language.
// Null while the deck's translations are off.
export function initialLanguage(
  code: string,
  translations: GuestTranslations | null,
): StartLanguage | null {
  if (!translations) return null;
  const phone = typeof navigator === 'undefined' ? [] : navigator.languages;
  return startLanguage(translations, phone, read(storageKey(code)));
}

// What the following screen needs to offer the language choice (SW-883 D2, D3).
export type GuestLanguageControl = {
  translations: GuestTranslations;
  // The language the phone is reading in: the state's, else the one it asked for, else the primary.
  current: string;
  // The language the phone asked for (its start or its last pick), or null before any. Re-reads send it.
  wanted: string | null;
  // Lines on the slide on screen that show in the primary language.
  fallbackLines: number;
  // Whether the one-time "Showing the slides in" banner is up.
  banner: boolean;
  dismissBanner(): void;
  // Moves the phone to this language and keeps the pick for the session. Rejects when the hub refuses; the phone then
  // stays in its current language.
  pick(code: string): Promise<void>;
};

// The language choice for the following screen, or null while translations are off. setLanguage is useAudience's.
export function useGuestLanguage(
  code: string,
  translations: GuestTranslations | null,
  language: AudienceLanguage | null,
  start: StartLanguage | null,
  setLanguage: (language: string) => Promise<void>,
): GuestLanguageControl | null {
  const [banner, setBanner] = useState(
    () => start?.fromPhone === true && read(bannerKey(code)) === null,
  );
  const [wanted, setWanted] = useState(start?.code ?? null);
  // The banner shows once per session: showing it is what marks it seen, so a reload doesn't bring it back.
  useEffect(() => {
    if (banner) write(bannerKey(code), '1');
  }, [banner, code]);
  const dismissBanner = useCallback(() => {
    write(bannerKey(code), '1');
    setBanner(false);
  }, [code]);
  const pick = useCallback(
    async (next: string) => {
      await setLanguage(next);
      setWanted(next);
      write(storageKey(code), next);
      write(bannerKey(code), '1');
      setBanner(false);
    },
    [code, setLanguage],
  );
  if (!translations) return null;
  return {
    translations,
    current: language?.code ?? wanted ?? translations.primary,
    wanted,
    fallbackLines: language?.fallbackLines ?? 0,
    banner,
    dismissBanner,
    pick,
  };
}

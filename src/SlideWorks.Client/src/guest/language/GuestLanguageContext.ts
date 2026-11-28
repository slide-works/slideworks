import { createContext, useContext } from 'react';

// The language the phone asked for (its start or its pick), or null while the deck's translations are off or it asked for
// none. It doesn't depend on the audience state, whose language is null in the lobby and after the talk. The guest's re-reads
// (poll, answer, open response) send it as ?language=.
export const GuestLanguageContext = createContext<string | null>(null);

export const useGuestLanguageCode = () => useContext(GuestLanguageContext);

// The query string a re-read adds: empty with no language.
export const languageQuery = (language: string | null | undefined) =>
  language ? `?language=${encodeURIComponent(language)}` : '';

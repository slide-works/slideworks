import { createContext, useContext } from 'react';

// The language the phone's state is composed in, or null while the deck's translations are off. The guest's re-reads
// (poll, answer, open response) send it as ?language=.
export const GuestLanguageContext = createContext<string | null>(null);

export const useGuestLanguageCode = () => useContext(GuestLanguageContext);

// The query string a re-read adds: empty with no language.
export const languageQuery = (language: string | null | undefined) =>
  language ? `?language=${encodeURIComponent(language)}` : '';

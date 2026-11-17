import { createContext, useContext } from 'react';

// The ask sheet over the Questions tab (Q8, D7). Frozen: GuestTabs provides it, the sheet, the Ask bar and the banner
// read it, and AskSheetFixture gives it in specs.
export type AskSheet = {
  open: boolean;
  // Opens the sheet (the Ask button).
  show(): void;
  // Closes it; the draft stays with the sheet.
  close(): void;
  // The banner's "Go to poll": closes the sheet and shows the Slide tab.
  goToPoll(): void;
  // True from a poll opening while the sheet is open until the sheet closes: the sheet shows the banner.
  pollJustOpened: boolean;
};

export const AskSheetContext = createContext<AskSheet | null>(null);

export function useAskSheet(): AskSheet {
  const sheet = useContext(AskSheetContext);
  if (sheet === null)
    throw new Error('useAskSheet needs a GuestTabs or an AskSheetFixture above it.');
  return sheet;
}

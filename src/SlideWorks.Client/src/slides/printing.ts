import { createContext, createElement, useContext, type ReactNode } from 'react';

// True inside the print page, where a slide is drawn for the PDF: no live results feed, no video player.
const PrintingContext = createContext(false);

export function PrintingProvider({ children }: { children: ReactNode }) {
  return createElement(PrintingContext.Provider, { value: true }, children);
}

export function usePrinting(): boolean {
  return useContext(PrintingContext);
}

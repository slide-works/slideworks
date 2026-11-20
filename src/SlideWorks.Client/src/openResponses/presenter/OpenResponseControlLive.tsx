import { useMemo, type ReactNode } from 'react';
import { OpenResponseControlContext, useOpenResponseControl } from './useOpenResponseControl';

export type OpenResponseControlLiveProps = {
  deckId: string;
  // The presenter's slide, 1-based.
  slide: number;
  children: ReactNode;
};

// Holds the presenter hook for the presenter window and the phone remote. Children read it with useOpenResponsePanel.
export function OpenResponseControlLive({ deckId, slide, children }: OpenResponseControlLiveProps) {
  const value = useOpenResponseControl(deckId, slide);
  const source = useMemo(() => ({ deckId, value }), [deckId, value]);
  return <OpenResponseControlContext value={source}>{children}</OpenResponseControlContext>;
}

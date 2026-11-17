import { useMemo, type ReactNode } from 'react';
import { QaControlContext, type QaControlSource } from './useQaControl';

export type QaControlLiveProps = {
  deckId: string;
  children: ReactNode;
};

// The presenter's Q&A controls in the presenter window, on the phone remote and on the deck page (SW-9 Q9), for a
// person who can present the deck. The server works out `here` from the presenter's slide, so no slide is passed in.
// TODO: always control null and the actions do nothing; the real provider watches the deck's audience, reads
// GET /api/decks/{id}/qa, follows questionControl dropping a lower seq, and sends each action and settings change
// through qaControlApi, showing a refusal or failure as error.
export function QaControlLive({ deckId, children }: QaControlLiveProps) {
  const source = useMemo<QaControlSource>(() => {
    const nothing = () => Promise.resolve();
    return {
      deckId,
      value: {
        control: null,
        approve: nothing,
        hide: nothing,
        restore: nothing,
        answer: nothing,
        reopen: nothing,
        show: nothing,
        unshow: nothing,
        setSettings: nothing,
        setSlide: nothing,
        error: null,
      },
    };
  }, [deckId]);
  return <QaControlContext value={source}>{children}</QaControlContext>;
}

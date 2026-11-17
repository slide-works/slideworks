import { useMemo, type ReactNode } from 'react';
import { QaControlContext, type QaControlSource } from './useQaControl';

export type QaControlLiveProps = {
  deckId: string;
  // The presenter's slide, 1-based, from SW-4's presentation state; null on the deck page, which has no presenter.
  slide: number | null;
  children: ReactNode;
};

// The presenter's Q&A controls in the presenter window, on the phone remote and on the deck page (SW-9 Q9), for a
// person who can present the deck. The view's `here` is the switch of the presenter's slide, but nothing publishes
// questionControl when the presenter moves, so the provider reads GET /api/decks/{id}/qa again whenever `slide`
// changes, as SW-6's PollControlLive does.
// TODO: always control null and the actions do nothing; the real provider watches the deck's audience, reads
// GET /api/decks/{id}/qa (again on every change of `slide`), follows questionControl dropping a lower seq, and sends
// each action and settings change through qaControlApi, showing a refusal or failure as error.
export function QaControlLive({ deckId, slide, children }: QaControlLiveProps) {
  const source = useMemo<QaControlSource>(() => {
    const nothing = () => Promise.resolve();
    return {
      deckId,
      slide,
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
  }, [deckId, slide]);
  return <QaControlContext value={source}>{children}</QaControlContext>;
}

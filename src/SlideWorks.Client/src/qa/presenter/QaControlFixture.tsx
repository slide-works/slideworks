import { useMemo, type ReactNode } from 'react';
import type { QaAction, QaControlView, QaSettingsPatch, QaSlidePatch } from '../types';
import { QaControlContext, type QaControlSource } from './useQaControl';

// One call a part made through the fixture: a moderation action on a question, a settings change or a slide's switch.
export type QaControlCall =
  | { action: QaAction; number: number }
  | { action: 'settings'; patch: QaSettingsPatch }
  | { action: 'slide'; slide: number; patch: QaSlidePatch };

// Tests only: provides the presenter's Q&A controls with a fixed view and error, as QaControlLive does once it has read
// them. Build `control` from qaControlView() in test-support/fixtures/qaStates.ts ("live": review on, question 2 on
// screen, question 4 waiting, question 5 hidden, slide 3's questions off; or "notLive"), with overrides; null is "not
// read yet". Each action calls onAction with what it was asked and changes nothing: mount again (or update) for the
// next state. `slide` is the presenter's slide the provider follows (null by default, as on the deck page). Frozen after
// wave 0.2.
export function QaControlFixture({
  deckId,
  slide = null,
  control,
  error = null,
  onAction,
  children,
}: {
  deckId: string;
  slide?: number | null;
  control: QaControlView | null;
  error?: string | null;
  onAction?: (call: QaControlCall) => void;
  children: ReactNode;
}) {
  const source = useMemo<QaControlSource>(() => {
    const record = (call: QaControlCall) => {
      onAction?.(call);
      return Promise.resolve();
    };
    const act = (action: QaAction) => (number: number) => record({ action, number });
    return {
      deckId,
      slide,
      value: {
        control,
        approve: act('approve'),
        hide: act('hide'),
        restore: act('restore'),
        answer: act('answer'),
        reopen: act('reopen'),
        show: act('show'),
        unshow: act('unshow'),
        setSettings: (patch) => record({ action: 'settings', patch }),
        setSlide: (slide, patch) => record({ action: 'slide', slide, patch }),
        error,
      },
    };
  }, [deckId, slide, control, error, onAction]);
  return <QaControlContext value={source}>{children}</QaControlContext>;
}

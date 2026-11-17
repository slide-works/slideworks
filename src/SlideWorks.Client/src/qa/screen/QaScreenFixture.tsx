import type { ReactNode } from 'react';
import type { QaControlView } from '../types';
import { QaScreenProvider, qaScreenOf } from './QaScreenContext';

// Tests only: provides the big screen's questions from a presenter's view, as QaScreenLive does once it has read it.
// Build `control` from qaControlView() in test-support/fixtures/qaStates.ts ("live" has question 2 on screen; pass
// { onScreen: null } for the list, or "notLive"). null is "no live data". Frozen after wave 0.2.
export function QaScreenFixture({
  control,
  children,
}: {
  control: QaControlView | null;
  children: ReactNode;
}) {
  return (
    <QaScreenProvider screen={control === null ? null : qaScreenOf(control)}>
      {children}
    </QaScreenProvider>
  );
}

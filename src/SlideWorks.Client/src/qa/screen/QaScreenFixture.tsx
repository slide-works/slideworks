import type { ReactNode } from 'react';
import type { QaControlView } from '../types';
import { QaScreenProvider, qaScreenStateOf } from './QaScreenContext';

// Tests only: provides the big screen's questions from a presenter's view, as QaScreenLive does once it has read it.
// Build `control` from qaControlView() in test-support/fixtures/qaStates.ts ("live" has question 2 on screen; pass
// { onScreen: null } for the list, or "notLive"). null is "no live data" (none); a live view with no round is "off". Frozen after wave 0.2.
export function QaScreenFixture({
  control,
  children,
}: {
  control: QaControlView | null;
  children: ReactNode;
}) {
  return (
    <QaScreenProvider state={control === null ? { kind: 'none' } : qaScreenStateOf(control)}>
      {children}
    </QaScreenProvider>
  );
}

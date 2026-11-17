import type { ReactNode } from 'react';
import type { PollControlView } from '../polls/types';
import { PollScreenProvider, pollScreenOf } from './PollContext';

// Tests only: provides the poll slide's live screen from a presenter's poll view, as PollScreenLive does once it has
// read it. Build `control` from pollControlView() in test-support/fixtures/pollStates.ts ("draft", "open" or
// "closed", with overrides for hidden counts, a shown answer or pick several); its `slide` picks the slide that gets
// it. null is "no live data". Frozen after wave 0.1.
export function PollScreenFixture({
  control,
  children,
}: {
  control: PollControlView | null;
  children: ReactNode;
}) {
  return (
    <PollScreenProvider source={control === null ? null : pollScreenOf(control)}>
      {children}
    </PollScreenProvider>
  );
}

import type { ReactNode } from 'react';
import { AudiencePollsContext } from '../guest/AudiencePollsContext';
import type { GuestView } from '../guest/guestView';
import { useAudience } from '../guest/useAudience';
import { AudienceQuestionsContext } from '../qa/guest/AudienceQuestionsContext';

// Component tests only: runs useAudience for the code and provides its poll and questions channels to children the way
// GuestLive does, without the guest screens, and shows the connection's status and the state's room (as JSON). Pair it
// with stubDeckHub's audience option.
export function AudienceHarness({
  code,
  view,
  children,
}: {
  code: string;
  view: GuestView;
  children: ReactNode;
}) {
  const { state, status, polls, questions } = useAudience(code, view);
  return (
    <AudiencePollsContext value={polls}>
      <AudienceQuestionsContext value={questions}>
        <p data-testid="harness-status">{status}</p>
        <p data-testid="harness-room">{JSON.stringify(state.room)}</p>
        {children}
      </AudienceQuestionsContext>
    </AudiencePollsContext>
  );
}

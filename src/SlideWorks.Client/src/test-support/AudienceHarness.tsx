import type { ReactNode } from 'react';
import { AudiencePollsContext } from '../guest/AudiencePollsContext';
import type { GuestView } from '../guest/guestView';
import { useAudience } from '../guest/useAudience';

// Component tests only: runs useAudience for the code and provides its poll channel to children the way GuestLive does,
// without the guest screens, and shows the connection's status. Pair it with stubDeckHub's audience option.
export function AudienceHarness({
  code,
  view,
  children,
}: {
  code: string;
  view: GuestView;
  children: ReactNode;
}) {
  const { status, polls } = useAudience(code, view);
  return (
    <AudiencePollsContext value={polls}>
      <p data-testid="harness-status">{status}</p>
      {children}
    </AudiencePollsContext>
  );
}

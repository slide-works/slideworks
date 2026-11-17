import { createContext, useContext } from 'react';
import type { AudiencePolls } from './useAudience';

// The audience connection's poll channel (useAudience's polls), provided by GuestLive around the guest's screens, so
// ActivitySlot can hand it to the poll card without every screen between them carrying it.
export const AudiencePollsContext = createContext<AudiencePolls | null>(null);

// The poll channel, or null outside GuestLive.
export function useAudiencePolls(): AudiencePolls | null {
  return useContext(AudiencePollsContext);
}

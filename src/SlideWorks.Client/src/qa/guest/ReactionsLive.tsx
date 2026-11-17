import { useMemo, type ReactNode } from 'react';
import type { AudienceStatus } from '../../guest/useAudience';
import type { QaRoom } from '../types';
import { PhoneReactionsContext, type PhoneReactions } from './usePhoneReactions';

export type ReactionsLiveProps = {
  // The join code the page is on, for POST /api/join/{code}/reactions.
  code: string;
  // The audience state's room: enabled follows room.reactions.
  room: QaRoom | null;
  status: AudienceStatus;
  children: ReactNode;
};

// The phone's reactions (Q7, Q8), given to the row through usePhoneReactions.
// TODO: a stub that stays disabled; the real provider follows room.reactions and sends taps.
export function ReactionsLive({ children }: ReactionsLiveProps) {
  const value = useMemo<PhoneReactions>(
    () => ({ enabled: false, cooling: false, error: null, tap: async () => undefined }),
    [],
  );
  return <PhoneReactionsContext value={value}>{children}</PhoneReactionsContext>;
}

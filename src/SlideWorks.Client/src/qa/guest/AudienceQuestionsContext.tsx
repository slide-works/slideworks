import { createContext, useContext } from 'react';
import type { AudienceQuestions } from '../../guest/useAudience';

// The audience connection's questions channel (useAudience's questions), provided by GuestLive beside the poll channel,
// so QuestionsLive can follow questionTally without every screen between them carrying it.
export const AudienceQuestionsContext = createContext<AudienceQuestions | null>(null);

// The questions channel, or null outside GuestLive (a spec that mounts the guest screens alone): then no tally arrives.
export function useAudienceQuestions(): AudienceQuestions | null {
  return useContext(AudienceQuestionsContext);
}

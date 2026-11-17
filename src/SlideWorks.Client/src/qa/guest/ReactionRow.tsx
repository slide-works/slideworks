import { QA_REACTION_ROW_TEST_IDS } from '../testIds';
import { usePhoneReactions } from './usePhoneReactions';

// The five reactions under the slide on the Slide tab, from usePhoneReactions. Absent when enabled is false.
// TODO: a stub with its root test id; the reaction row replaces it.
export function ReactionRow() {
  const { enabled } = usePhoneReactions();
  if (!enabled) return null;
  return <div data-testid={QA_REACTION_ROW_TEST_IDS.row} role="group" aria-label="Reactions" />;
}

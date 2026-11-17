import { QA_LIST_TEST_IDS } from '../testIds';

// The Ask button under the list, which stays in view and opens the sheet with useAskSheet().show(). Asking is replaced by
// the list's closed or off-here line when the room can't ask.
// TODO: a stub with its root test id; the Ask bar replaces it.
export function AskBar() {
  return <div data-testid={QA_LIST_TEST_IDS.askBar} />;
}

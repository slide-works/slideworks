import { QA_POLL_OPENED_TEST_IDS } from '../testIds';

// The banner in the ask sheet when a poll opens while it is open (D7), with "Go to poll" calling useAskSheet().goToPoll().
// TODO: a stub with its root test id; the ask sheet's banner replaces it.
export function PollOpenedBanner() {
  return <div data-testid={QA_POLL_OPENED_TEST_IDS.banner} />;
}

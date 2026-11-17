import { POLL_CARD_TEST_IDS } from '../testIds';

// Offline or reconnecting with an unsent pick: "Not sent yet". The pick stays on the phone and goes by itself when the
// connection is back, if the poll is still open.
export function PollQueued() {
  // TODO: draw the "Not sent yet" note; until then it is one line.
  return <p data-testid={POLL_CARD_TEST_IDS.queued}>Not sent yet</p>;
}

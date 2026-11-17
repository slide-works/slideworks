import { POLL_CARD_TEST_IDS } from '../testIds';

// Offline or reconnecting with an unsent pick: "Not sent yet". The pick stays on the phone and goes by itself when the
// connection is back, if the poll is still open. There is no Send button: nothing for the guest to do.
export function PollQueued() {
  return (
    <div data-testid={POLL_CARD_TEST_IDS.queued} className="grid gap-2">
      <p
        role="status"
        data-testid={POLL_CARD_TEST_IDS.queuedMessage}
        className="text-[13px] font-semibold text-(--sw-warn)"
      >
        Not sent yet
      </p>
      <p className="rounded-lg bg-(--sw-paper) px-2.5 py-2 text-[12px] leading-snug text-(--sw-muted)">
        Your pick is kept on this phone. It sends by itself when the connection is back.
      </p>
    </div>
  );
}

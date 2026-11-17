import { useEffect, useState } from 'react';
import { POLL_CARD_TEST_IDS } from '../testIds';

// Offline or reconnecting with an unsent pick: "Not sent yet". The pick stays on the phone and goes by itself when the
// connection is back, if the poll is still open. There is no Send button: nothing for the guest to do.
export function PollQueued() {
  // A live region is announced when its text changes, not when it arrives with its text, so it mounts empty and is
  // filled once the card is on screen.
  const [said, setSaid] = useState('');
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- filling after mount is what makes it announce
    setSaid('Not sent yet');
  }, []);
  return (
    <div data-testid={POLL_CARD_TEST_IDS.queued} className="grid gap-2">
      <p
        role="status"
        data-testid={POLL_CARD_TEST_IDS.queuedMessage}
        className="min-h-[1lh] text-[13px] font-semibold text-(--sw-warn-ink)"
      >
        {said}
      </p>
      <p className="rounded-lg bg-(--sw-paper) px-2.5 py-2 text-[12px] leading-snug text-(--sw-muted)">
        Your pick is kept on this phone. It sends by itself when the connection is back.
      </p>
    </div>
  );
}

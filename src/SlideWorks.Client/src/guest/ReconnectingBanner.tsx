import { GUEST_TEST_IDS } from './testIds';

// Over the last screen while the connection comes back. The page catches up silently afterwards.
export function ReconnectingBanner() {
  // TODO: the real amber banner, styled to the mockups.
  return (
    <div data-testid={GUEST_TEST_IDS.reconnecting} role="status">
      Reconnecting
    </div>
  );
}

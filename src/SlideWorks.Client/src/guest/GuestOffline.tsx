import { GUEST_TEST_IDS } from './testIds';

// Live updates can't reach the phone (every transport failed, or it is offline). It explains and retries.
export function GuestOffline({ onRetry }: { onRetry: () => void }) {
  // TODO: the real screen, styled to the mockups.
  return (
    <main data-testid={GUEST_TEST_IDS.offline}>
      <h1>Can't reach the talk</h1>
      <button type="button" data-testid={GUEST_TEST_IDS.offlineRetry} onClick={onRetry}>
        Try again
      </button>
    </main>
  );
}

import { GUEST_TEST_IDS } from './testIds';

// Over the last screen while the connection comes back. The page catches up silently afterwards.
export function ReconnectingBanner() {
  return (
    <div
      data-testid={GUEST_TEST_IDS.reconnecting}
      role="status"
      className="fixed inset-x-3 top-3 z-20 flex items-center gap-2.5 rounded-xl bg-[#9a3412] px-3.5 py-3 text-sm font-medium text-white shadow-lg"
    >
      <span
        aria-hidden
        className="size-4 shrink-0 animate-spin rounded-full border-2 border-white/40 border-t-white motion-reduce:animate-none"
      />
      Reconnecting… you'll catch up by yourself.
    </div>
  );
}

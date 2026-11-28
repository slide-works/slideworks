import type { ReactNode } from 'react';
import type { GuestView } from './guestView';
import { GUEST_TEST_IDS } from './testIds';

// The following screen's header: the deck's title, the presenter, and the Live chip, which reads Offline while the
// connection comes back, and the language chip when the deck has translations. Hidden in landscape, where the slide fills the screen.
export function GuestFollowingHeader({
  view,
  reconnecting,
  languageChip = null,
}: {
  view: GuestView;
  reconnecting: boolean;
  // The language chip, shown next to the Live chip; null while the deck has no translations.
  languageChip?: ReactNode | null;
}) {
  return (
    <header className="flex items-center gap-3 [@media(orientation:landscape)_and_(max-height:500px)]:hidden">
      <div className="min-w-0 flex-1">
        <h1
          data-testid={GUEST_TEST_IDS.deckTitle}
          className="truncate font-head text-[13px] leading-tight font-bold"
        >
          {view.deckTitle}
        </h1>
        <p
          data-testid={GUEST_TEST_IDS.presenter}
          className="truncate text-[11px] text-(--sw-night-muted)"
        >
          {view.presenterName}
        </p>
      </div>
      {languageChip}
      <span
        data-testid={GUEST_TEST_IDS.chip}
        className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ${reconnecting ? 'bg-[rgb(234_88_12/.2)] text-[#fdba74]' : 'bg-[rgb(22_163_74/.18)] text-[#86efac]'}`}
      >
        <span
          aria-hidden
          className={`size-1.5 rounded-full ${reconnecting ? 'bg-[#fb923c]' : 'bg-[#4ade80]'}`}
        />
        {reconnecting ? 'Offline' : 'Live'}
      </span>
    </header>
  );
}

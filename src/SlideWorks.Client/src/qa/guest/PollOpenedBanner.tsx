import { QA_POLL_OPENED_TEST_IDS } from '../testIds';
import { useAskSheet } from './AskSheetContext';

export const POLL_OPENED_TEXT = 'A poll just opened. It is on the Slide tab.';

// The banner in the ask sheet when a poll opens while it is open (D7), with "Go to poll" calling useAskSheet().goToPoll().
// The sheet shows it while useAskSheet().pollJustOpened, and says its message through its own calm status region.
export function PollOpenedBanner() {
  const { goToPoll } = useAskSheet();
  return (
    <div
      data-testid={QA_POLL_OPENED_TEST_IDS.banner}
      className="flex items-center justify-between gap-3 rounded-[10px] bg-[rgb(165_184_255/0.12)] px-3 py-2.5 text-[12px] leading-[1.4] text-[#dbe3ff]"
    >
      <span data-testid={QA_POLL_OPENED_TEST_IDS.message}>{POLL_OPENED_TEXT}</span>
      <button
        type="button"
        data-testid={QA_POLL_OPENED_TEST_IDS.goToPoll}
        onClick={goToPoll}
        className="min-h-11 shrink-0 cursor-pointer rounded-lg px-2 text-[13px] font-bold text-white underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
      >
        Go to poll
      </button>
    </div>
  );
}

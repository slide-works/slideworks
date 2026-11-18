import { QA_LIST_TEST_IDS } from '../testIds';
import { useAskSheet } from './AskSheetContext';
import { useQuestions } from './useQuestions';

// The Ask button under the list, which stays in view and opens the sheet with useAskSheet().show(). It shows only once a
// read has said the room can ask: before the first read lands (and after a failed one) the room is unknown, and asking
// is replaced by the list's closed or off-here line when the room can't ask.
export function AskBar() {
  const { room } = useQuestions();
  const sheet = useAskSheet();
  if (room?.questions !== 'open') return null;
  return (
    <div data-testid={QA_LIST_TEST_IDS.askBar} className="flex shrink-0 flex-col">
      <button
        type="button"
        data-testid={QA_LIST_TEST_IDS.ask}
        onClick={sheet.show}
        className="flex min-h-[46px] cursor-pointer items-center justify-center rounded-xl bg-(--sw-accent) px-3 text-[15px] font-bold text-(--sw-on-accent) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent-glow)"
      >
        Ask a question
      </button>
    </div>
  );
}

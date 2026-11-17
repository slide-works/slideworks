import { QA_ASK_SHEET_TEST_IDS } from '../testIds';
import { useAskSheet } from './AskSheetContext';

export type AskSheetProps = {
  presenter: string;
};

// The sheet over the list while useAskSheet().open: the text, the name row (absent when room.names is false), Send, and
// the sending, queued, limited and failed states from useQuestions. It shows PollOpenedBanner while pollJustOpened.
// TODO: a stub with its root test id while open; the ask sheet replaces it.
export function AskSheet({ presenter }: AskSheetProps) {
  const { open } = useAskSheet();
  if (!open) return null;
  return (
    <div
      data-testid={QA_ASK_SHEET_TEST_IDS.sheet}
      role="dialog"
      aria-label="Ask a question"
      data-presenter={presenter}
    />
  );
}

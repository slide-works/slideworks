import { useEffect, useRef, useState } from 'react';
import { QA_STRIP_TEST_IDS, qaStripSheetActionTestId, qaStripSheetRowTestId } from '../testIds';
import type { QaAction, QaQuestion } from '../types';

// The moderation actions the remote offers: the panel's rows without Restore and Reopen, which are for hidden and
// answered questions (not listed here).
export type StripAction = Extract<QaAction, 'approve' | 'hide' | 'show' | 'unshow' | 'answer'>;

type Props = {
  // The open and to-review questions, in the order to show.
  questions: QaQuestion[];
  error: string | null;
  // Runs the action; settles once the answer is in.
  onAction: (action: StripAction, number: number) => Promise<void>;
  onClose: () => void;
};

const LABELS: Record<StripAction, string> = {
  approve: 'Approve',
  hide: 'Hide',
  show: 'Show',
  unshow: 'Take off',
  answer: 'Answered',
};

// The buttons a waiting or open question allows, as the panel's rows (D13), the first being the one to press.
function actionsOf(question: QaQuestion): StripAction[] {
  if (question.state === 'waiting') return ['approve', 'hide'];
  return [question.onScreen ? 'unshow' : 'show', 'answer', 'hide'];
}

const BUTTON =
  'min-h-11 min-w-11 rounded-lg px-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-300 forced-colors:border-2';

// The sheet behind "All 6" on the phone remote: the open and to-review questions, each with its buttons as the panel's
// rows. A modal dialog, so the page behind is inert and Escape closes it; closing returns focus to the button that
// opened it. A moderation action keeps focus in the sheet: on the row that took the place of the one that went.
export function QaStripSheet({ questions, error, onAction, onClose }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);
  const rows = useRef<HTMLUListElement>(null);
  const sending = useRef(false);
  const pending = useRef<number | null>(null);
  const [settled, setSettled] = useState(0);

  useEffect(() => {
    const element = dialog.current;
    if (element !== null && !element.open) element.showModal();
  }, []);

  const run = async (action: StripAction, number: number) => {
    if (sending.current) return;
    sending.current = true;
    const items = [...(rows.current?.children ?? [])] as HTMLElement[];
    const index = items.findIndex((item) => item.dataset.number === String(number));
    try {
      await onAction(action, number);
    } finally {
      sending.current = false;
    }
    pending.current = index;
    setSettled((n) => n + 1);
  };

  // Once the answer is drawn: if the pressed button is gone with its row, focus goes to the button of the row that took
  // its place (or the last), else to Close, never to the page.
  useEffect(() => {
    const index = pending.current;
    const element = dialog.current;
    if (index === null || element === null) return;
    pending.current = null;
    if (element.contains(document.activeElement)) return;
    const now = [...(rows.current?.children ?? [])] as HTMLElement[];
    const target = now[Math.min(index, now.length - 1)]?.querySelector('button');
    (
      target ??
      element.querySelector<HTMLElement>(`[data-testid="${QA_STRIP_TEST_IDS.sheetClose}"]`)
    )?.focus();
  }, [settled, questions]);

  return (
    <dialog
      ref={dialog}
      data-testid={QA_STRIP_TEST_IDS.sheet}
      aria-label="Questions"
      onClose={onClose}
      className="fixed inset-x-0 top-auto bottom-0 m-0 flex max-h-[85dvh] w-full max-w-none flex-col gap-2 rounded-t-2xl border border-gray-800 bg-[#111827] p-3 text-slate-100 backdrop:bg-black/60"
    >
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold">Questions</h2>
        <button
          type="button"
          data-testid={QA_STRIP_TEST_IDS.sheetClose}
          aria-label="Close the list of questions"
          onClick={() => dialog.current?.close()}
          className={`${BUTTON} border border-white/25 bg-white/10`}
        >
          Close
        </button>
      </div>
      {error !== null && (
        <p
          data-testid={QA_STRIP_TEST_IDS.sheetError}
          role="alert"
          className="rounded-md bg-[#c0362c]/25 px-3 py-2 text-sm text-red-200"
        >
          {error}
        </p>
      )}
      <ul
        ref={rows}
        data-testid={QA_STRIP_TEST_IDS.sheetRows}
        className="grid min-h-0 gap-2 overflow-y-auto"
      >
        {questions.length === 0 && (
          <li className="py-2 text-sm text-slate-400">No open or waiting questions.</li>
        )}
        {questions.map((question) => (
          <li
            key={question.number}
            data-testid={qaStripSheetRowTestId(question.number)}
            data-number={question.number}
            className="grid gap-1.5 rounded-lg bg-white/[0.04] p-2.5"
          >
            <p className="text-sm leading-snug [overflow-wrap:anywhere]">{question.text}</p>
            <p className="text-xs text-slate-400">
              {question.votes} {question.votes === 1 ? 'vote' : 'votes'}
              {question.state === 'waiting' && ' · To review'}
              {question.onScreen && ' · On screen now'}
            </p>
            <div className="flex flex-wrap gap-1.5">
              {actionsOf(question).map((action, index) => (
                <button
                  key={action}
                  type="button"
                  data-testid={qaStripSheetActionTestId(action, question.number)}
                  aria-label={`${LABELS[action]}: ${question.text}`}
                  onClick={() => void run(action, question.number)}
                  className={`${BUTTON} ${index === 0 ? 'bg-[#2f5bea] text-white' : 'bg-white/10 text-slate-100'}`}
                >
                  {LABELS[action]}
                </button>
              ))}
            </div>
          </li>
        ))}
      </ul>
    </dialog>
  );
}

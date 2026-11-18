import {
  qaPanelActionTestId,
  qaPanelRowMetaTestId,
  qaPanelRowTestId,
  qaPanelRowTextTestId,
  qaPanelRowVotesTestId,
} from '../testIds';
import type { QaAction, QaQuestion, QaShowOn } from '../types';

type Props = {
  question: QaQuestion;
  showOn: QaShowOn;
  onAction: (action: QaAction, number: number) => void;
};

// The buttons a question's state allows (D13), in the order they show: the first is the one the room is waiting for.
function rowActions(question: QaQuestion): QaAction[] {
  if (question.hidden) return ['restore'];
  switch (question.state) {
    case 'waiting':
      return ['approve', 'hide'];
    case 'answered':
      return ['reopen'];
    case 'open':
      return [question.onScreen ? 'unshow' : 'show', 'answer', 'hide'];
  }
}

function labelOf(action: QaAction, showOn: QaShowOn): string {
  switch (action) {
    case 'approve':
      return 'Approve';
    case 'hide':
      return 'Hide';
    case 'restore':
      return 'Restore';
    case 'answer':
      return 'Answered';
    case 'reopen':
      return 'Reopen';
    case 'show':
      return showOn === 'qaSlide' ? 'Show on Q&A slide' : 'Show on screen';
    case 'unshow':
      return 'Take off screen';
  }
}

// The button the room is waiting for is filled; the rest are quiet.
const PRIMARY: ReadonlySet<QaAction> = new Set(['approve', 'show']);

// One question in the Questions panel (SW-9 Q9): its votes, its text as plain text, "{name or Anonymous} · {age}" with
// " · On screen now" while it is up, and the buttons its state allows, each named with its question ("Hide: How do you
// split a big change?").
export function QaPanelRow({ question, showOn, onAction }: Props) {
  const { number, text, onScreen } = question;
  const up = onScreen && !question.hidden;
  return (
    <li
      data-testid={qaPanelRowTestId(number)}
      data-number={number}
      className={`grid grid-cols-[34px_minmax(0,1fr)] gap-2.5 rounded-[9px] border px-2.5 py-2 ${
        up ? 'border-[#4ade80] bg-[#16a34a]/10' : 'border-transparent bg-white/[0.04]'
      }`}
    >
      <div
        data-testid={qaPanelRowVotesTestId(number)}
        className="grid h-9 content-center justify-items-center gap-0.5 rounded-[7px] bg-[#8ea5ff]/15 font-mono text-[13px] leading-none text-[#c7d2fe]"
      >
        <span aria-hidden="true" className="text-[8px] text-[#94a3b8]">
          ▲
        </span>
        {question.votes}
        <span className="sr-only">{question.votes === 1 ? ' vote' : ' votes'}</span>
      </div>
      <div className="min-w-0">
        <p
          data-testid={qaPanelRowTextTestId(number)}
          className="text-[12.5px] leading-[1.35] [overflow-wrap:anywhere] text-[#f1f5f9]"
        >
          {text}
        </p>
        <p data-testid={qaPanelRowMetaTestId(number)} className="mt-0.5 text-[11px] text-[#94a3b8]">
          {question.name ?? 'Anonymous'} · {question.age}
          {up && (
            <>
              {' · '}
              <b className="font-semibold text-[#86efac]">On screen now</b>
            </>
          )}
        </p>
        <div className="mt-1.5 flex flex-wrap gap-1.5">
          {rowActions(question).map((action) => (
            <button
              key={action}
              type="button"
              data-testid={qaPanelActionTestId(action, number)}
              aria-label={`${labelOf(action, showOn)}: ${text}`}
              onClick={() => onAction(action, number)}
              className={`min-h-8 rounded-md px-2.5 py-1.5 text-[11.5px] font-semibold focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#8ea5ff] forced-colors:border forced-colors:border-[ButtonText] ${
                PRIMARY.has(action)
                  ? 'bg-[#2f5bea] text-white hover:bg-[#3b67f0]'
                  : 'bg-white/[0.07] text-[#e2e8f0] hover:bg-white/[0.12]'
              }`}
            >
              {labelOf(action, showOn)}
            </button>
          ))}
        </div>
      </div>
    </li>
  );
}

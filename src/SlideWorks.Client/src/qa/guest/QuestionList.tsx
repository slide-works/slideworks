import { useCalmAnnouncement } from '../../polls/guest/useCalmAnnouncement';
import { QA_LIST_TEST_IDS } from '../testIds';
import type { GuestQuestion } from '../types';
import { QuestionRow } from './QuestionRow';
import { QuestionsEmpty } from './QuestionsEmpty';
import { questionErrorText } from './questionErrorText';
import { useQuestions, type QuestionSort } from './useQuestions';

export type QuestionListProps = {
  // The presenter's name, as the copy names them ("Waiting for Sam", "Sam has closed questions.").
  presenter: string;
};

// Top: most votes first, ties to the older question (the lower number). New: newest first.
const byTop = (a: GuestQuestion, b: GuestQuestion) => b.votes - a.votes || a.number - b.number;
const byNew = (a: GuestQuestion, b: GuestQuestion) => b.number - a.number;

const CARD =
  'flex min-h-0 flex-1 flex-col gap-2 overflow-hidden rounded-[14px] bg-white px-3 pt-3 pb-2.5 text-(--sw-ink)';
const NOTE =
  'shrink-0 rounded-lg bg-[#f6f7f9] px-2.5 py-2 text-[12px] leading-[1.4] text-(--sw-muted)';

const SORTS: { sort: QuestionSort; label: string; testId: string }[] = [
  { sort: 'top', label: 'Top', testId: QA_LIST_TEST_IDS.sortTop },
  { sort: 'new', label: 'New', testId: QA_LIST_TEST_IDS.sortNew },
];

// The Questions tab's list in every state, from useQuestions: the header and sort, the guest's own waiting and hidden
// questions pinned on top, the open ones, the answered section, and the loading, failed, empty, closed and off-here
// states. It renders QuestionRow and QuestionsEmpty; AskBar sits under it.
export function QuestionList({ presenter }: QuestionListProps) {
  const { room, questions, state, error, errorFrom, retry, sort, setSort } = useQuestions();

  const open = questions.filter((q) => q.state === 'open');
  // The open count changes only when the list does (a tally moves votes, not states); a screen reader hears it at most
  // once every 5 seconds.
  const said = useCalmAnnouncement(
    open.length === 1 ? '1 open question' : `${open.length} open questions`,
  );
  // An ask's failure shows in the ask sheet; the list shows a read's or an upvote's.
  const shownError = error !== null && errorFrom !== 'ask' ? error : null;

  if (state === 'loading')
    return (
      <section
        data-testid={QA_LIST_TEST_IDS.list}
        aria-label="Questions"
        aria-busy
        className={CARD}
      >
        <p
          data-testid={QA_LIST_TEST_IDS.loading}
          className="grid flex-1 place-items-center text-[12.5px] text-(--sw-muted)"
        >
          Loading the questions…
        </p>
      </section>
    );

  if (state === 'failed')
    return (
      <section data-testid={QA_LIST_TEST_IDS.list} aria-label="Questions" className={CARD}>
        <div
          data-testid={QA_LIST_TEST_IDS.failed}
          role="alert"
          className="grid flex-1 place-items-center content-center gap-2 px-2.5 text-center"
        >
          <p className="font-head text-[15px] leading-[1.3] font-bold">
            Couldn't load the questions.
          </p>
          {shownError !== null && (
            <p className="text-[12.5px] leading-[1.45] text-(--sw-muted)">
              {questionErrorText(shownError, { presenter })}
            </p>
          )}
          <button
            type="button"
            data-testid={QA_LIST_TEST_IDS.failedRetry}
            onClick={retry}
            className="min-h-11 cursor-pointer rounded-[10px] border-[1.5px] border-(--sw-grid) bg-[#f6f7f9] px-4 text-[13.5px] font-bold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent)"
          >
            Try again
          </button>
        </div>
      </section>
    );

  // The guest's own questions nobody else sees, in number order, then the open ones in the chosen sort.
  const pinned = questions
    .filter((q) => q.yours && (q.state === 'waiting' || q.state === 'hidden'))
    .sort((a, b) => a.number - b.number);
  const shown = [...pinned, ...open.sort(sort === 'top' ? byTop : byNew)];
  const answered = questions.filter((q) => q.state === 'answered').sort(byTop);
  return (
    <section data-testid={QA_LIST_TEST_IDS.list} aria-label="Questions" className={CARD}>
      <p aria-live="polite" className="sr-only">
        {said}
      </p>
      {questions.length === 0 ? (
        <QuestionsEmpty presenter={presenter} />
      ) : (
        <>
          <header className="flex shrink-0 items-center gap-2">
            <h2
              id="qa-list-heading"
              data-testid={QA_LIST_TEST_IDS.heading}
              className="font-head text-[15px] leading-[1.2] font-bold"
            >
              Questions
            </h2>
            <span
              data-testid={QA_LIST_TEST_IDS.openCount}
              aria-hidden
              className="text-[12px] leading-none font-semibold whitespace-nowrap text-(--sw-muted)"
            >
              {`${open.length} open`}
            </span>
            <div
              role="group"
              aria-label="Sort"
              data-testid={QA_LIST_TEST_IDS.sort}
              className="ml-auto inline-flex rounded-lg border border-(--sw-grid) bg-[#f6f7f9] p-0.5"
            >
              {SORTS.map((s) => (
                <button
                  key={s.sort}
                  type="button"
                  aria-pressed={sort === s.sort}
                  data-testid={s.testId}
                  onClick={() => setSort(s.sort)}
                  className="min-h-11 min-w-11 cursor-pointer rounded-md px-2.5 text-[11.5px] leading-none font-semibold text-(--sw-muted) focus-visible:outline-2 focus-visible:outline-(--sw-accent) aria-pressed:bg-white aria-pressed:text-(--sw-ink) aria-pressed:shadow-[0_1px_2px_rgb(15_23_42/.12)]"
                >
                  {s.label}
                </button>
              ))}
            </div>
          </header>
          <div className="-mx-1 min-h-0 flex-1 overflow-y-auto overscroll-contain px-1">
            <ul data-testid={QA_LIST_TEST_IDS.rows}>
              {shown.map((q) => (
                <QuestionRow key={q.number} question={q} presenter={presenter} />
              ))}
            </ul>
            {answered.length > 0 && (
              <section
                data-testid={QA_LIST_TEST_IDS.answered}
                aria-labelledby="qa-list-answered-heading"
                className="border-t border-(--sw-grid)"
              >
                <h3
                  id="qa-list-answered-heading"
                  data-testid={QA_LIST_TEST_IDS.answeredHeading}
                  className="pt-2.5 pb-0.5 text-[10.5px] leading-none font-semibold tracking-[.08em] text-(--sw-muted) uppercase"
                >
                  {`Answered · ${answered.length}`}
                </h3>
                <ul>
                  {answered.map((q) => (
                    <QuestionRow key={q.number} question={q} presenter={presenter} />
                  ))}
                </ul>
              </section>
            )}
          </div>
        </>
      )}
      {shownError !== null && (
        <p
          data-testid={QA_LIST_TEST_IDS.error}
          role="alert"
          className="shrink-0 rounded-lg bg-(--sw-bad-soft) px-2.5 py-2 text-[12px] leading-[1.4] text-(--sw-bad-ink)"
        >
          {questionErrorText(shownError, { presenter })}
        </p>
      )}
      {room?.questions === 'closed' && (
        <p data-testid={QA_LIST_TEST_IDS.closed} className={NOTE}>
          {`${presenter} has closed questions. You can still upvote.`}
        </p>
      )}
      {room?.questions === 'offHere' && (
        <p data-testid={QA_LIST_TEST_IDS.offHere} className={NOTE}>
          Questions are off on this slide. You can still upvote.
        </p>
      )}
    </section>
  );
}

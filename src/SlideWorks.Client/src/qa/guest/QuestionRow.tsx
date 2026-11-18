import {
  questionAnsweredTestId,
  questionHiddenTestId,
  questionMetaTestId,
  questionNoVoteTestId,
  questionOnScreenTestId,
  questionRowTestId,
  questionTextTestId,
  questionUpvoteTestId,
  questionVotesTestId,
  questionWaitingTestId,
  questionYouTestId,
} from '../testIds';
import type { GuestQuestion } from '../types';
import { useQuestions } from './useQuestions';

export type QuestionRowProps = {
  question: GuestQuestion;
  presenter: string;
};

const votesText = (votes: number) => (votes === 1 ? '1 vote' : `${votes} votes`);

const TAG =
  'rounded-[4px] px-[5px] py-[3px] text-[9.5px] leading-none font-bold tracking-[.06em] uppercase not-italic';

// What stands in place of the upvote on a question that can't be upvoted: the guest's own waiting (…) and hidden (–)
// ones, and an answered one (✓). Decoration only: the row's tags and meta say the same in words.
const HELD: Partial<
  Record<GuestQuestion['state'], { mark: string; testId: (n: number) => string; className: string }>
> = {
  waiting: { mark: '…', testId: questionNoVoteTestId, className: 'border-dashed text-[#b45309]' },
  hidden: { mark: '–', testId: questionNoVoteTestId, className: 'border-dashed text-(--sw-muted)' },
  answered: {
    mark: '✓',
    testId: questionAnsweredTestId,
    className: 'border-transparent bg-(--sw-good-soft) text-(--sw-good-ink)',
  },
};

// One question in the list: the upvote toggle (from useQuestions' mine and toggleVote), its text, its meta and its tags.
// Only an open question has the toggle; its count is the hook's, so the row shows it as is. The list never shows who
// upvoted. Text and name are text nodes. The row takes focus from code (tabIndex -1), so focus can move to a new
// question after sending.
export function QuestionRow({ question, presenter }: QuestionRowProps) {
  const { mine, toggleVote } = useQuestions();
  const { number, text, name, age, state, votes, onScreen, yours } = question;
  const held = HELD[state];
  const pressed = mine.picks.includes(number);
  const dim = state === 'answered' || state === 'hidden';

  return (
    <li
      data-testid={questionRowTestId(number)}
      tabIndex={-1}
      className="grid grid-cols-[44px_minmax(0,1fr)] items-start gap-2.5 border-t border-(--sw-grid) py-2 outline-none first:border-t-0 focus-visible:outline-2 focus-visible:outline-(--sw-accent)"
    >
      {held ? (
        <span
          aria-hidden
          data-testid={held.testId(number)}
          className={`grid min-h-11 w-11 place-items-center rounded-[10px] border-[1.5px] border-(--sw-grid) text-[15px] font-bold ${held.className}`}
        >
          {held.mark}
        </span>
      ) : (
        <button
          type="button"
          data-testid={questionUpvoteTestId(number)}
          aria-pressed={pressed}
          aria-label={`Upvote, ${votesText(votes)}: ${text}`}
          onClick={() => toggleVote(number)}
          className={`grid min-h-11 w-11 cursor-pointer place-items-center content-center gap-0.5 rounded-[10px] border-[1.5px] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--sw-accent) ${pressed ? 'border-(--sw-accent) bg-(--sw-accent-light) text-(--sw-accent)' : 'border-(--sw-grid) bg-white text-(--sw-muted)'}`}
        >
          {/* Filled when upvoted, an outline when not, so the state isn't carried by colour alone. */}
          <span aria-hidden className="text-[9px] leading-none">
            {pressed ? '▲' : '△'}
          </span>
          <span
            aria-hidden
            data-testid={questionVotesTestId(number)}
            className={`text-[13.5px] leading-none font-bold tabular-nums ${pressed ? 'text-(--sw-accent-dark)' : 'text-(--sw-ink)'}`}
          >
            {votes}
          </span>
        </button>
      )}
      <div className="min-w-0">
        <p
          data-testid={questionTextTestId(number)}
          className={`text-[13px] leading-[1.32] font-medium [overflow-wrap:anywhere] ${dim ? 'text-(--sw-muted)' : 'text-(--sw-ink)'}`}
        >
          {text}
        </p>
        <div className="mt-[3px] flex flex-wrap items-center gap-x-1.5 gap-y-1 text-[11px] text-(--sw-muted)">
          {yours && (
            <em
              data-testid={questionYouTestId(number)}
              className={`${TAG} bg-(--sw-accent-light) text-(--sw-accent-dark)`}
            >
              You
            </em>
          )}
          <span className="[overflow-wrap:anywhere]">
            <span
              data-testid={questionMetaTestId(number)}
            >{`${name ?? 'Anonymous'} · ${age}`}</span>
            {state === 'answered' && (
              <span data-testid={questionVotesTestId(number)}>{` · ${votesText(votes)}`}</span>
            )}
          </span>
          {state === 'waiting' && (
            <em
              data-testid={questionWaitingTestId(number)}
              className={`${TAG} bg-[#fef3c7] text-[#92400e]`}
            >{`Waiting for ${presenter}`}</em>
          )}
          {state === 'hidden' && (
            <em
              data-testid={questionHiddenTestId(number)}
              className={`${TAG} bg-[#f1f2f5] text-(--sw-muted)`}
            >
              Not shown to the room
            </em>
          )}
          {onScreen && state === 'open' && (
            <em
              data-testid={questionOnScreenTestId(number)}
              className={`${TAG} bg-(--sw-good-soft) text-(--sw-good-ink)`}
            >
              On screen now
            </em>
          )}
        </div>
      </div>
    </li>
  );
}

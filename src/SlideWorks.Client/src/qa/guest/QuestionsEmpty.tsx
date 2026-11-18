import { QA_LIST_TEST_IDS } from '../testIds';
import { useQuestions } from './useQuestions';

export type QuestionsEmptyProps = {
  presenter: string;
};

// The list with no questions yet. The Ask button under it is the only thing to do. When the room can't ask (closed or
// off on this slide) only the title shows: the list's closed or off-here line says why.
export function QuestionsEmpty({ presenter }: QuestionsEmptyProps) {
  const { room } = useQuestions();
  const canAsk = room?.questions === 'open';
  return (
    <div
      data-testid={QA_LIST_TEST_IDS.empty}
      className="grid flex-1 place-items-center content-center gap-1.5 px-2.5 text-center"
    >
      <p
        data-testid={QA_LIST_TEST_IDS.emptyTitle}
        className="font-head text-[15px] leading-[1.3] font-bold"
      >
        No questions yet
      </p>
      {canAsk && (
        <p
          data-testid={QA_LIST_TEST_IDS.emptyText}
          className="text-[12.5px] leading-[1.45] text-(--sw-muted)"
        >
          {`Ask the first one. Other people can upvote it, and ${presenter} sees which questions the room wants answered.`}
        </p>
      )}
    </div>
  );
}

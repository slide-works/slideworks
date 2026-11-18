import { QA_LIST_TEST_IDS } from '../testIds';

export type QuestionsEmptyProps = {
  presenter: string;
};

// The list with no questions yet. The Ask button under it is the only thing to do.
export function QuestionsEmpty({ presenter }: QuestionsEmptyProps) {
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
      <p
        data-testid={QA_LIST_TEST_IDS.emptyText}
        className="text-[12.5px] leading-[1.45] text-(--sw-muted)"
      >
        {`Ask the first one. Other people can upvote it, and ${presenter} sees which questions the room wants answered.`}
      </p>
    </div>
  );
}

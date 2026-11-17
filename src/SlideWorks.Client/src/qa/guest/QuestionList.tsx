import { QA_LIST_TEST_IDS } from '../testIds';

export type QuestionListProps = {
  // The presenter's name, as the copy names them ("Waiting for Sam", "Sam has closed questions.").
  presenter: string;
};

// The Questions tab's list in every state, from useQuestions: the header and sort, the guest's own waiting and hidden
// questions pinned on top, the open ones, the answered section, and the loading, failed, empty, closed and off-here
// states. It renders QuestionRow and QuestionsEmpty; AskBar sits under it.
// TODO: a stub with its root test id; the list replaces it.
export function QuestionList({ presenter }: QuestionListProps) {
  return (
    <section
      data-testid={QA_LIST_TEST_IDS.list}
      aria-label="Questions"
      data-presenter={presenter}
    />
  );
}

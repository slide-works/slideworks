import { QA_LIST_TEST_IDS } from '../testIds';

export type QuestionsEmptyProps = {
  presenter: string;
};

// The list with no questions yet.
// TODO: a stub with its root test id; the list's empty state replaces it.
export function QuestionsEmpty({ presenter }: QuestionsEmptyProps) {
  return <div data-testid={QA_LIST_TEST_IDS.empty} data-presenter={presenter} />;
}

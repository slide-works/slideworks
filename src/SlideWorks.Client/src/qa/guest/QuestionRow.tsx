import { questionRowTestId } from '../testIds';
import type { GuestQuestion } from '../types';

export type QuestionRowProps = {
  question: GuestQuestion;
  presenter: string;
};

// One question in the list: the upvote toggle (from useQuestions' mine and toggleVote), its text, its meta and its tags.
// TODO: a stub with its root test id; the list's row replaces it.
export function QuestionRow({ question }: QuestionRowProps) {
  return <li data-testid={questionRowTestId(question.number)} />;
}

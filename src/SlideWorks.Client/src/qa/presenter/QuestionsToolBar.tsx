import { ToolBar, type ToolBarProps } from '../../presentation/presenter/ToolBar';
import { useQaControl } from './useQaControl';

// SW-4's tool bar in the presenter window with the Questions tool's count (SW-9): the questions waiting or open, from
// the presenter's Q&A view, so a presenter with the panel closed sees questions arriving. No count before the view is
// read. Only under a QaControlLive or QaControlFixture for deckId.
export function QuestionsToolBar({ deckId, ...props }: ToolBarProps & { deckId: string }) {
  const { control } = useQaControl(deckId);
  const count = control === null ? undefined : control.counts.waiting + control.counts.open;
  return <ToolBar {...props} questionsCount={count} />;
}

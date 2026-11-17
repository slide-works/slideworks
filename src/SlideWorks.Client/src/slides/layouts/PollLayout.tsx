import { POLL_SLIDE_TEST_IDS, pollSlideRowTestId } from '../../polls/testIds';
import type { LayoutProps } from '../slots';

// The poll slide (light, SW-6 V7): the question, one row per option and, with live data from usePollScreen, a bar and
// a percentage per option and a footer with the state and the answer count.
export function PollLayout({ slots }: LayoutProps<'poll'>) {
  // TODO: question and options only. The real layout adds the bars, the footer, the join pill and usePollScreen.
  return (
    <div data-testid={POLL_SLIDE_TEST_IDS.slide} className="sw-layout">
      <h2 data-testid={POLL_SLIDE_TEST_IDS.question}>{slots.question}</h2>
      <div className="sw-body">
        <ol data-testid={POLL_SLIDE_TEST_IDS.rows}>
          {slots.options.map((option, position) => (
            <li key={position} data-testid={pollSlideRowTestId(position)}>
              {option}
            </li>
          ))}
        </ol>
      </div>
    </div>
  );
}

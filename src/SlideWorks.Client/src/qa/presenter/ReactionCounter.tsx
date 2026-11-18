import { useCalmAnnouncement } from '../../polls/guest/useCalmAnnouncement';
import { REACTIONS } from '../emoji';
import { QA_PANEL_TEST_IDS as IDS, qaPanelReactionCountTestId } from '../testIds';
import type { ReactionCounts } from '../types';

type Props = {
  // The deck's reactions setting.
  enabled: boolean;
  // The session's totals per emoji, as useQaControl gives them (the view's totals plus the batches since).
  counts: ReactionCounts;
};

// The reactions counter in the Questions panel (SW-9 D6): each emoji the room has sent with its total ("👍 3  ❤️ 1"),
// "No reactions yet", or "Reactions off". The totals change often, so a screen reader hears them through a polite live
// region at most once every 5 seconds, never on every batch.
export function ReactionCounter({ enabled, counts }: Props) {
  const sent = REACTIONS.filter(({ key }) => counts[key] > 0);
  const said = useCalmAnnouncement(
    !enabled
      ? 'Reactions off'
      : sent.length === 0
        ? 'No reactions yet'
        : `Reactions: ${sent.map(({ key, name }) => `${counts[key]} ${name}`).join(', ')}`,
  );
  return (
    <div
      data-testid={IDS.counter}
      className="ml-auto font-mono text-[12px] leading-none whitespace-nowrap text-[#e2e8f0]"
    >
      <span aria-hidden="true" className="flex gap-3">
        {!enabled ? (
          <span data-testid={IDS.counterOff} className="text-[#94a3b8]">
            Reactions off
          </span>
        ) : sent.length === 0 ? (
          <span data-testid={IDS.counterEmpty} className="text-[#94a3b8]">
            No reactions yet
          </span>
        ) : (
          sent.map(({ key, emoji }) => (
            <span key={key} data-testid={qaPanelReactionCountTestId(key)}>
              {emoji} {counts[key]}
            </span>
          ))
        )}
      </span>
      <span role="status" aria-live="polite" className="sr-only">
        {said}
      </span>
    </div>
  );
}

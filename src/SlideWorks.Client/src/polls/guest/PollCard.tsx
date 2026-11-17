import { useEffect, useId, useRef, useState } from 'react';
import type { PollActivity } from '../types';
import { POLL_CARD_TEST_IDS } from '../testIds';
import { PollConflict } from './PollConflict';
import { PollFoot } from './PollFoot';
import { PollOptions, type PollBars } from './PollOptions';
import { PollOptionsMany } from './PollOptionsMany';
import { PollQueued } from './PollQueued';
import { PollVerdict } from './PollVerdict';
import { usePollAnswer } from './usePollAnswer';

const kindOf = (activity: PollActivity) =>
  activity.pick === 'one' ? 'Poll · Pick one' : `Poll · Pick up to ${activity.max}`;

const samePicks = (a: number[], b: number[]) =>
  a.length === b.length && a.every((pick, i) => pick === b[i]);

// The poll on a guest's phone (V6): its kind, round and question, the options, and under them the foot, or the
// conflict or the queued note in its place, then the verdict once the answer is shown. The options scroll past five and
// the foot stays in view. A phone never shows counts before its guest has answered, whatever the setting; once the
// poll has closed it shows the final result to everyone.
export function PollCard({ activity, presenter }: { activity: PollActivity; presenter: string }) {
  const answer = usePollAnswer();
  const questionId = useId();
  const mine = answer.mine?.picks ?? null;
  // Pick several: once an answer is sent the boxes lock until "Change my answer". The edit holds for this round and this
  // answer's revision, so a new accepted answer or a new round locks them again without any reset.
  const editKey = `${activity.round ?? ''}:${answer.mine?.revision ?? 0}`;
  const [editing, setEditing] = useState<string | null>(null);
  const unlocked = editing === editKey;
  const locked =
    activity.pick === 'upTo' && mine !== null && samePicks(answer.picks, mine) && !unlocked;
  const list = useRef<HTMLDivElement>(null);
  useEffect(() => {
    // Change my answer unlocks the boxes and the button may go: focus moves to the first box that can be ticked.
    if (unlocked) {
      const boxes = list.current?.querySelectorAll<HTMLInputElement>('input:not(:disabled)');
      (
        list.current?.querySelector<HTMLInputElement>('input:checked:not(:disabled)') ?? boxes?.[0]
      )?.focus();
    }
  }, [unlocked]);
  const bars: PollBars | null =
    answer.tally?.counts && (mine !== null || activity.phase === 'closed')
      ? { counts: answer.tally.counts, answered: answer.tally.answered }
      : null;
  const options = {
    questionId,
    activity,
    disabled: answer.state === 'loading' || activity.phase !== 'open' || locked,
    picks: answer.picks,
    mine,
    bars,
    onToggle: answer.toggle,
  };

  let below;
  if (answer.state === 'conflict')
    below = (
      <PollConflict
        activity={activity}
        presenter={presenter}
        onSend={answer.send}
        onClear={answer.clear}
      />
    );
  else if (answer.state === 'queued') below = <PollQueued />;
  else
    below = (
      <PollFoot
        activity={activity}
        presenter={presenter}
        picks={answer.picks}
        mine={answer.mine}
        answered={answer.tally?.answered ?? null}
        joinedLate={answer.joinedLate}
        state={answer.state}
        error={answer.error}
        onSend={answer.send}
        onClear={() => {
          setEditing(editKey);
          answer.clear();
        }}
      />
    );

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-2">
      <article
        data-testid={POLL_CARD_TEST_IDS.card}
        aria-labelledby={questionId}
        className="flex min-h-0 flex-1 flex-col gap-3 rounded-[14px] bg-white p-4 text-[#0f172a] shadow-[0_10px_30px_rgb(0_0_0/.25)]"
      >
        <header className="flex shrink-0 flex-col gap-2">
          <div className="flex items-center justify-between gap-2 text-[10px] font-bold tracking-[.08em] uppercase">
            <p
              data-testid={POLL_CARD_TEST_IDS.kind}
              className="rounded-[4px] bg-[#eef2ff] px-1.5 py-0.5 text-[#3730a3]"
            >
              {kindOf(activity)}
            </p>
            {activity.number > 1 && (
              <p data-testid={POLL_CARD_TEST_IDS.round} className="text-[#475569] normal-case">
                {`Round ${activity.number}`}
              </p>
            )}
          </div>
          <h2
            id={questionId}
            data-testid={POLL_CARD_TEST_IDS.question}
            className="font-head text-[17px] leading-snug font-bold"
          >
            {activity.question}
          </h2>
        </header>
        <div ref={list} className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          {activity.pick === 'one' ? (
            <PollOptions {...options} />
          ) : (
            <PollOptionsMany {...options} />
          )}
        </div>
        <div className="flex shrink-0 flex-col gap-2">
          {below}
          {activity.correct !== null && (
            <PollVerdict correct={activity.correct} options={activity.options} mine={mine} />
          )}
        </div>
      </article>
      <p
        data-testid={POLL_CARD_TEST_IDS.privacy}
        className="shrink-0 px-1 text-[11px] text-(--sw-night-muted)"
      >
        {`Private: ${presenter} sees totals only`}
      </p>
    </div>
  );
}

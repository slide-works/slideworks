import { useState } from 'react';
import { REACTIONS } from '../emoji';
import { QA_REACTION_ROW_TEST_IDS, reactionButtonTestId } from '../testIds';
import type { ReactionEmoji } from '../types';
import { questionErrorText } from './questionErrorText';
import styles from './ReactionRow.module.css';
import { usePhoneReactions } from './usePhoneReactions';

export type ReactionRowProps = {
  // The presenter's name, for questionErrorText's copy.
  presenter: string;
};

// The five reactions under the activity card on the Slide tab (Q8, D6; mockup phone state 1), from usePhoneReactions.
// A tap sends one and pops its emoji (not under reduced motion). Greyed while cooling; absent when enabled is false. A
// failed tap's message shows under the row as an alert. Hidden in the landscape slide-only layout, as the
// activity card is.
export function ReactionRow({ presenter }: ReactionRowProps) {
  const { enabled, tap, cooling, error } = usePhoneReactions();
  // The last tapped emoji, and a count that remounts the pop so a second tap starts it again.
  const [pop, setPop] = useState<{ emoji: ReactionEmoji; taps: number } | null>(null);
  if (!enabled) return null;
  return (
    <div className="flex shrink-0 flex-col [@media(orientation:landscape)_and_(max-height:500px)]:hidden">
      <div
        role="group"
        aria-label="Reactions"
        data-testid={QA_REACTION_ROW_TEST_IDS.row}
        className={`grid grid-cols-5 gap-1.5 ${cooling ? 'opacity-40' : ''}`}
      >
        {REACTIONS.map((reaction) => (
          <button
            key={reaction.key}
            type="button"
            data-testid={reactionButtonTestId(reaction.key)}
            aria-label={`React ${reaction.name}`}
            disabled={cooling}
            onClick={() => {
              setPop((last) => ({ emoji: reaction.key, taps: (last?.taps ?? 0) + 1 }));
              void tap(reaction.key);
            }}
            className="relative grid min-h-11 place-items-center rounded-xl bg-white/10 text-xl leading-none transition-transform focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-(--sw-accent-glow) active:scale-90 disabled:cursor-default motion-reduce:duration-0 motion-reduce:active:scale-100"
          >
            <span aria-hidden>{reaction.emoji}</span>
            {pop?.emoji === reaction.key && (
              <span
                key={pop.taps}
                aria-hidden
                data-testid={QA_REACTION_ROW_TEST_IDS.pop}
                className={`${styles.pop} motion-reduce:hidden`}
              >
                {reaction.emoji}
              </span>
            )}
          </button>
        ))}
      </div>
      {error !== null && (
        <p
          data-testid={QA_REACTION_ROW_TEST_IDS.error}
          role="alert"
          className="mt-1.5 rounded-lg bg-(--sw-bad-soft) px-2.5 py-2 text-[12px] leading-[1.4] text-(--sw-bad-ink)"
        >
          {questionErrorText(error, { presenter })}
        </p>
      )}
    </div>
  );
}

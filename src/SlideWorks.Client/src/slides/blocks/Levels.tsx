import type { LevelsBlock } from '../slots';
import styles from './Levels.module.css';

type LevelsProps = { levels: LevelsBlock };

// A ladder of 2–7 levels, top rung first, in a 480 px card. Any rung can be
// current: it is highlighted and carries aria-current. A rung without a mark
// draws an empty circle. The card clips its overflow, so a too-wide font never
// draws text over the blocks beside it.
export function Levels({ levels }: LevelsProps) {
  const { title, items } = levels;
  return (
    <div data-testid="block-levels" className={styles.levels}>
      {title !== undefined ? (
        <div data-testid="levels-title" className={styles.title}>
          {title}
        </div>
      ) : null}
      <ol className={styles.rungs}>
        {items.map((item, i) => {
          const current = item.state === 'current';
          return (
            <li
              key={i}
              data-testid="levels-rung"
              data-state={current ? 'current' : 'plain'}
              aria-current={current ? 'true' : undefined}
              className={styles.rung}
            >
              <span data-testid="levels-mark" className={styles.mark}>
                {item.mark}
              </span>
              <span className={styles.text}>{item.text}</span>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

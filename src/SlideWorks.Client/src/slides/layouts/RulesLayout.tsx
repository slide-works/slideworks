import type { ReactNode } from 'react';
import { SlideHeading } from '../blocks/SlideHeading';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import styles from './RulesLayout.module.css';

// A headline over 1–4 rules, each a large outline numeral beside a title and
// text. With reveal 'one-by-one' rule n appears at step n.
export function RulesLayout({ slots, step }: LayoutProps<'rules'>) {
  const { kicker, headline, rules, reveal } = slots;
  return (
    <div data-testid="layout-rules" className="sw-layout">
      <SlideHeading layout="rules" kicker={kicker} headline={headline} />
      <div data-testid="rules-body" className="sw-body">
        <ol className={styles.rules}>
          {rules.map((rule, i) => (
            <Item key={i} at={reveal === 'one-by-one' ? i + 1 : null} step={step}>
              <span data-testid="rules-number" className={styles.number} aria-hidden>
                {i + 1}
              </span>
              <div>
                <h3 data-testid="rules-title" className={styles.title}>
                  {rule.title}
                </h3>
                <p data-testid="rules-text" className={styles.text}>
                  {rule.text}
                </p>
              </div>
            </Item>
          ))}
        </ol>
      </div>
    </div>
  );
}

// One rule, a list item: revealed at its step, or shown from the start when
// `at` is null.
function Item({ at, step, children }: { at: number | null; step: number; children: ReactNode }) {
  return at === null ? (
    <li className={styles.rule}>{children}</li>
  ) : (
    <Reveal as="li" at={at} step={step} className={styles.rule}>
      {children}
    </Reveal>
  );
}

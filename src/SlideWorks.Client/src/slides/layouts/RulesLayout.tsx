import { SlideHeading } from '../blocks/SlideHeading';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import styles from './RulesLayout.module.css';

// A headline over 1–4 rules, each a large outline numeral beside a title and
// text. With reveal 'one-by-one' rule n appears at step n;
// otherwise every rule shows from the start (at 0).
export function RulesLayout({ slots, step }: LayoutProps<'rules'>) {
  const { kicker, headline, rules, reveal } = slots;
  return (
    <div data-testid="layout-rules" className="sw-layout">
      <SlideHeading layout="rules" kicker={kicker} headline={headline} />
      <div data-testid="rules-body" className="sw-body">
        <ol className={styles.rules}>
          {rules.map((rule, i) => (
            <Reveal
              key={i}
              as="li"
              at={reveal === 'one-by-one' ? i + 1 : 0}
              step={step}
              className={styles.rule}
            >
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
            </Reveal>
          ))}
        </ol>
      </div>
    </div>
  );
}

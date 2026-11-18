import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import styles from './RulesLayout.module.css';

// A headline over 1–4 rules, each a large outline numeral beside a title and
// text. With reveal 'one-by-one' rule n appears at step n;
// otherwise every rule shows from the start (at 0). A callout shows from the
// start or, with reveal 'last', as the step after the last rule.
export function RulesLayout({ slots, step }: LayoutProps<'rules'>) {
  const { kicker, headline, rules, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  return (
    <div data-testid="layout-rules" className="sw-layout">
      <SlideHeading
        layout="rules"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div data-testid="rules-body" className="sw-body">
        <ol className={styles.rules}>
          {rules.map((rule, i) => (
            <Reveal key={i} as="li" at={oneByOne ? i + 1 : 0} step={step} className={styles.rule}>
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
        {callout !== undefined ? (
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(oneByOne ? rules.length : 0, callout)}
          />
        ) : null}
      </div>
    </div>
  );
}

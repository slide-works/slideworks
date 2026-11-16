import { Callout } from '../blocks/Callout';
import { LeadText } from '../blocks/LeadText';
import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import styles from './PracticeLayout.module.css';

// A practice: what to do on the left; what comes back (good) and what to
// avoid (bad) on the right, with an optional callout under them. An empty
// list is left out with its header.
export function PracticeLayout({ slots }: LayoutProps<'practice'>) {
  const { kicker, headline, do: doItems, comes_back: comesBack, avoid, callout } = slots;
  return (
    <div data-testid="layout-practice" className="sw-layout">
      <SlideHeading layout="practice" kicker={kicker} headline={headline} />
      <div data-testid="practice-body" className={`sw-body ${styles.columns}`}>
        <div className={styles.column}>
          <section data-testid="practice-do" className={styles.group}>
            <h3 className={styles.header}>Do</h3>
            {doItems.map((item, i) => (
              <p key={i} data-testid="practice-card" className={styles.card}>
                <LeadText item={item} leadClassName={styles.lead} />
              </p>
            ))}
          </section>
        </div>
        <div className={styles.column}>
          {comesBack.length > 0 ? (
            <section data-testid="practice-comes-back" className={styles.group}>
              <h3 className={styles.header} data-tone="good">
                What comes back
              </h3>
              {comesBack.map((text, i) => (
                <p key={i} data-testid="practice-card" data-tone="good" className={styles.card}>
                  {text}
                </p>
              ))}
            </section>
          ) : null}
          {avoid.length > 0 ? (
            <section data-testid="practice-avoid" className={styles.group}>
              <h3 className={styles.header} data-tone="bad">
                Avoid
              </h3>
              {avoid.map((text, i) => (
                <p key={i} data-testid="practice-card" data-tone="bad" className={styles.card}>
                  {text}
                </p>
              ))}
            </section>
          ) : null}
          {callout !== undefined ? (
            <div className={styles.callout}>
              <Callout tone={callout.tone} text={callout.text} />
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}

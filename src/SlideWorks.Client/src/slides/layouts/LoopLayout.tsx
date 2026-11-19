import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import styles from './LoopLayout.module.css';

// A cycle: the last step leads back to the first. Three steps sit in one row; more sit in two.
export function LoopLayout({ slots }: LayoutProps<'loop'>) {
  const { kicker, headline, steps } = slots;
  return (
    <div data-testid="layout-loop" className="sw-layout">
      <SlideHeading
        layout="loop"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <ol
        data-testid="loop-body"
        className={`sw-body ${styles.steps}`}
        data-rows={steps.length <= 3 ? '1' : '2'}
      >
        {steps.map((step, index) => (
          <li key={index} data-testid="loop-step" className={styles.step}>
            <span className={styles.label}>{step.label}</span>
            <span className={styles.text}>{step.text}</span>
          </li>
        ))}
      </ol>
      <p className={styles.back}>The last step leads back to the first.</p>
    </div>
  );
}

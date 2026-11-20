import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import { ComparePanel } from './ComparePanel';
import styles from './CompareLayout.module.css';

// Two panels side by side: the failure mode on the left and the success mode on
// the right, negative and positive unless a side sets its own tone (SW-2 L4).
// With reveal 'one-by-one' the success panel appears at step 1 (its space is
// kept) and the stamps of both panels together at step 2, when either has one.
// The callout, if there is one, sits under the panels, last when its reveal says so.
export function CompareLayout({ slots, step }: LayoutProps<'compare'>) {
  const { kicker, headline, failure, success, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  const stamps = failure.stamp !== undefined || success.stamp !== undefined;
  const stampAt = oneByOne ? 2 : 0;
  const successPanel = (
    <ComparePanel
      testId="compare-success"
      defaultTone="positive"
      side={success}
      lineSpans={emphasisFor(slots, 'success.line')}
      step={step}
      stampAt={stampAt}
    />
  );
  const panels = (
    <>
      <ComparePanel
        testId="compare-failure"
        defaultTone="negative"
        side={failure}
        lineSpans={emphasisFor(slots, 'failure.line')}
        step={step}
        stampAt={stampAt}
      />
      {oneByOne ? (
        <Reveal at={1} step={step} className={styles.slot}>
          {successPanel}
        </Reveal>
      ) : (
        successPanel
      )}
    </>
  );
  return (
    <div data-testid="layout-compare" className="sw-layout">
      <SlideHeading
        layout="compare"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      {callout === undefined ? (
        <div data-testid="compare-body" className={`sw-body ${styles.panels}`}>
          {panels}
        </div>
      ) : (
        <div data-testid="compare-body" className={`sw-body ${styles.withCallout}`}>
          <div className={`${styles.panels} ${styles.grow}`}>{panels}</div>
          <BodyCallout
            callout={callout}
            step={step}
            at={calloutStep(oneByOne ? (stamps ? 2 : 1) : 0, callout)}
          />
        </div>
      )}
    </div>
  );
}

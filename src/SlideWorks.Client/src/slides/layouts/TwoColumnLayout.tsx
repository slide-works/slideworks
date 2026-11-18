import { BodyCallout } from '../blocks/BodyCallout';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import { Reveal } from '../Reveal';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import { TwoColumnColumn } from './TwoColumnColumn';
import styles from './TwoColumnLayout.module.css';

// A headline over two columns. Each column has an optional title, with an
// optional icon beside it, and exactly one of a list of points, a paragraph or
// an uploaded image. With reveal 'one-by-one' the right column arrives at step
// 1 and keeps its space until then. An optional callout sits under both
// columns, from the start or, when its reveal is 'last', as the final step.
export function TwoColumnLayout({ slots, step }: LayoutProps<'two-column'>) {
  const { kicker, headline, left, right, reveal, callout } = slots;
  const oneByOne = reveal === 'one-by-one';
  return (
    <div data-testid="layout-two-column" className="sw-layout">
      <SlideHeading
        layout="two-column"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div data-testid="two-column-body" className={`sw-body ${styles.body}`}>
        <div className={styles.columns}>
          <TwoColumnColumn side="left" column={left} />
          <Reveal at={oneByOne ? 1 : 0} step={step} className={styles.right}>
            <TwoColumnColumn side="right" column={right} />
          </Reveal>
        </div>
        {callout !== undefined ? (
          <BodyCallout callout={callout} step={step} at={calloutStep(oneByOne ? 1 : 0, callout)} />
        ) : null}
      </div>
    </div>
  );
}

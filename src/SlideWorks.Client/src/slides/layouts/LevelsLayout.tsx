import { BodyCallout } from '../blocks/BodyCallout';
import { Facts } from '../blocks/Facts';
import { Levels } from '../blocks/Levels';
import { SlideHeading } from '../blocks/SlideHeading';
import { emphasisFor } from '../emphasis';
import type { LayoutProps } from '../slots';
import { calloutStep } from '../steps';
import styles from './LevelsLayout.module.css';
import { TwoColumnColumn } from './TwoColumnColumn';

// A ladder of levels with the current ones highlighted, beside a main column
// of points, a paragraph or an image. Fact tiles sit at the foot of the main
// column, and the callout under them. The ladder is on the right unless side
// is 'left'. Nothing arrives by step but a callout that reveals 'last'.
export function LevelsLayout({ slots, step }: LayoutProps<'levels'>) {
  const { kicker, headline, levels, main, facts, side, callout } = slots;
  const ladderFirst = side === 'left';
  const ladder = (
    <div data-testid="levels-ladder" className={styles.ladder}>
      <Levels levels={levels} />
    </div>
  );
  const column = (
    <div data-testid="levels-main" className={styles.main}>
      <div className={styles.content}>
        <TwoColumnColumn side="left" column={main} />
      </div>
      {facts !== undefined ? (
        <div data-testid="levels-tiles" className={styles.tiles}>
          <Facts facts={facts} />
        </div>
      ) : null}
      {callout !== undefined ? (
        <BodyCallout callout={callout} step={step} at={calloutStep(0, callout)} />
      ) : null}
    </div>
  );
  return (
    <div data-testid="layout-levels" className="sw-layout">
      <SlideHeading
        layout="levels"
        kicker={kicker}
        headline={headline}
        emphasis={emphasisFor(slots, 'headline')}
      />
      <div
        data-testid="levels-body"
        data-side={ladderFirst ? 'left' : 'right'}
        className={`sw-body ${styles.body}`}
      >
        {ladderFirst ? ladder : column}
        {ladderFirst ? column : ladder}
      </div>
    </div>
  );
}

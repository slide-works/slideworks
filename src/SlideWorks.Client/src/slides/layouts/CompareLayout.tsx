import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import { ComparePanel } from './ComparePanel';
import styles from './CompareLayout.module.css';

// Two panels side by side: the failure mode (bad, red) on the left and the
// success mode (good, green) on the right.
export function CompareLayout({ slots }: LayoutProps<'compare'>) {
  const { kicker, headline, failure, success } = slots;
  return (
    <div data-testid="layout-compare" className="sw-layout">
      <SlideHeading layout="compare" kicker={kicker} headline={headline} />
      <div data-testid="compare-body" className={`sw-body ${styles.panels}`}>
        <ComparePanel testId="compare-failure" tone="bad" side={failure} />
        <ComparePanel testId="compare-success" tone="good" side={success} />
      </div>
    </div>
  );
}

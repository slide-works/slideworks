import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import { ComparePanel } from './ComparePanel';
import styles from './CompareLayout.module.css';

// Two panels side by side: the failure mode on the left and the success mode on
// the right, negative and positive unless a side sets its own tone (SW-2 L4).
export function CompareLayout({ slots }: LayoutProps<'compare'>) {
  const { kicker, headline, failure, success } = slots;
  return (
    <div data-testid="layout-compare" className="sw-layout">
      <SlideHeading layout="compare" kicker={kicker} headline={headline} />
      <div data-testid="compare-body" className={`sw-body ${styles.panels}`}>
        <ComparePanel testId="compare-failure" defaultTone="negative" side={failure} />
        <ComparePanel testId="compare-success" defaultTone="positive" side={success} />
      </div>
    </div>
  );
}

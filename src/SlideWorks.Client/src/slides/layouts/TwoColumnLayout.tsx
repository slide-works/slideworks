import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import { TwoColumnColumn } from './TwoColumnColumn';
import styles from './TwoColumnLayout.module.css';

// A headline over two columns. Each column has an optional title and exactly
// one of a list of points, a paragraph or an uploaded image.
export function TwoColumnLayout({ slots }: LayoutProps<'two-column'>) {
  const { kicker, headline, left, right } = slots;
  return (
    <div data-testid="layout-two-column" className="sw-layout">
      <SlideHeading layout="two-column" kicker={kicker} headline={headline} />
      <div data-testid="two-column-body" className={`sw-body ${styles.columns}`}>
        <TwoColumnColumn side="left" column={left} />
        <TwoColumnColumn side="right" column={right} />
      </div>
    </div>
  );
}

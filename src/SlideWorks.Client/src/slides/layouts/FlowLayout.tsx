import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// The flow slide.
// TODO: draws only its kicker and headline; the real flow layout replaces it.
export function FlowLayout({ slots }: LayoutProps<'flow'>) {
  return (
    <div data-testid="layout-flow" className="sw-layout">
      <SlideHeading layout="flow" kicker={slots.kicker} headline={slots.headline} />
    </div>
  );
}

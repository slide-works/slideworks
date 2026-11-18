import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// The bars slide.
// TODO: draws only its kicker and headline; the real bars layout replaces it.
export function BarsLayout({ slots }: LayoutProps<'bars'>) {
  return (
    <div data-testid="layout-bars" className="sw-layout">
      <SlideHeading layout="bars" kicker={slots.kicker} headline={slots.headline} />
    </div>
  );
}

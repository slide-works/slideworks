import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// The links slide.
// TODO: draws only its kicker and headline; the real links layout replaces it.
export function LinksLayout({ slots }: LayoutProps<'links'>) {
  return (
    <div data-testid="layout-links" className="sw-layout">
      <SlideHeading layout="links" kicker={slots.kicker} headline={slots.headline} />
    </div>
  );
}

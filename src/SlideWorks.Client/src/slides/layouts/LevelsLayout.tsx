import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// The levels slide.
// TODO: draws only its kicker and headline; the real levels layout replaces it.
export function LevelsLayout({ slots }: LayoutProps<'levels'>) {
  return (
    <div data-testid="layout-levels" className="sw-layout">
      <SlideHeading layout="levels" kicker={slots.kicker} headline={slots.headline} />
    </div>
  );
}

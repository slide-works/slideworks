import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// The stack slide.
// TODO: draws only its kicker and headline; the real stack layout replaces it.
export function StackLayout({ slots }: LayoutProps<'stack'>) {
  return (
    <div data-testid="layout-stack" className="sw-layout">
      <SlideHeading layout="stack" kicker={slots.kicker} headline={slots.headline} />
    </div>
  );
}

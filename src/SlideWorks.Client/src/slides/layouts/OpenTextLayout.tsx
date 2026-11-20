import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// The open-text slide (light, SW-8): the question the room answers on their phones.
export function OpenTextLayout({ slots }: LayoutProps<'open-text'>) {
  return (
    <div data-testid="layout-open-text" className="sw-layout">
      <SlideHeading layout="open-text" headline={slots.question} />
      {/* TODO: the wall of answers replaces this empty body. */}
      <div data-testid="open-text-body" className="sw-body" />
    </div>
  );
}

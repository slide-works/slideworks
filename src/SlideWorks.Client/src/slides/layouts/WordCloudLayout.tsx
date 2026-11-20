import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';

// The word-cloud slide (light, SW-8): the question the room answers on their phones.
export function WordCloudLayout({ slots }: LayoutProps<'word-cloud'>) {
  return (
    <div data-testid="layout-word-cloud" className="sw-layout">
      <SlideHeading layout="word-cloud" headline={slots.question} />
      {/* TODO: the live word cloud replaces this empty body. */}
      <div data-testid="word-cloud-body" className="sw-body" />
    </div>
  );
}

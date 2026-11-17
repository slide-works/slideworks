import { SlideHeading } from '../blocks/SlideHeading';
import type { LayoutProps } from '../slots';
import { DEFAULT_QA_TITLE } from './qaTitle';

// The Q&A slide (light, SW-9 Q1): its title over the room's questions.
// TODO: draws only the title; the real Q&A slide (the list, the question on screen, the empty, closed and not-live states) replaces it.
export function QaLayout({ slots }: LayoutProps<'qa'>) {
  return (
    <div data-testid="layout-qa" className="sw-layout">
      <SlideHeading layout="qa" headline={slots.title ?? DEFAULT_QA_TITLE} />
      <div className="sw-body" />
    </div>
  );
}

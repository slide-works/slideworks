import type { SlideFinding } from '../../api/types';
import { SLIDE_CHECKS_TEST_IDS } from './slideChecksTestIds';

// The count of a slide's errors and warnings (notes aren't counted), drawn on its thumbnail. Nothing when there are none.
export function SlideChecksBadge({ slide, findings }: { slide: number; findings: SlideFinding[] }) {
  const problems = findings.filter((finding) => finding.severity !== 'note');
  if (problems.length === 0) return null;
  const hasError = problems.some((finding) => finding.severity === 'error');
  return (
    <span
      data-testid={SLIDE_CHECKS_TEST_IDS.badge}
      role="img"
      aria-label={`Slide ${slide}: ${problems.length} ${problems.length === 1 ? 'problem' : 'problems'}`}
      className={`rounded-full px-1.5 text-xs font-semibold ${
        hasError ? 'bg-bad text-panel' : 'bg-warn-soft text-ink'
      }`}
    >
      {problems.length}
    </span>
  );
}

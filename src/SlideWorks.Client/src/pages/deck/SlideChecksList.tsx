import type { SlideFinding } from '../../api/types';
import { SLIDE_CHECKS_TEST_IDS } from './slideChecksTestIds';

// The selected slide's findings, one row each, as text. Notes sit last (the list arrives sorted).
export function SlideChecksList({
  slide,
  findings,
  unchecked = false,
}: {
  slide: number;
  findings: SlideFinding[];
  unchecked?: boolean;
}) {
  return (
    <div data-testid={SLIDE_CHECKS_TEST_IDS.list} className="grid gap-1.5">
      <h4 className="text-xs font-semibold tracking-wide text-ink-muted uppercase">Checks</h4>
      {unchecked && (
        <p data-testid={SLIDE_CHECKS_TEST_IDS.unchecked} className="text-meta text-bad">
          Could not check slide {slide}.
        </p>
      )}
      {findings.length === 0 && !unchecked ? (
        <p data-testid={SLIDE_CHECKS_TEST_IDS.none} className="text-meta text-ink-faint">
          No problems found on this slide.
        </p>
      ) : findings.length === 0 ? null : (
        <ul className="grid gap-1.5 text-meta text-ink">
          {findings.map((finding, index) => (
            <li key={index} data-testid={SLIDE_CHECKS_TEST_IDS.row}>
              <span className={`font-semibold ${finding.severity === 'error' ? 'text-bad' : ''}`}>
                {finding.severity}
              </span>
              {': '}
              {finding.message} <span className="font-mono text-ink-muted">({finding.where})</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

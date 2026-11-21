import type { FindingRule, FindingSeverity, SlideFinding } from '../api/types';
import type { Canvas } from './canvas';

export type { DeckChecksView, FindingRule, FindingSeverity, SlideFinding } from '../api/types';

// The fixed severity of each rule (D5).
export function severityOf(rule: FindingRule): FindingSeverity {
  switch (rule) {
    case 'overflow':
    case 'fit':
    case 'imageNotLoaded':
    case 'renderFailed':
      return 'error';
    case 'textOnImage':
      return 'note';
    default:
      return 'warning';
  }
}

// One measured rule: reads a rendered slide frame, returns its findings.
export type MeasureRule = (frame: HTMLElement, slide: number, canvas: Canvas) => SlideFinding[];

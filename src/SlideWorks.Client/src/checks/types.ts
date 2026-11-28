import type { FindingRule, FindingSeverity, SlideFinding } from '../api/types';
import type { Canvas } from './canvas';

export type { DeckChecksView, FindingRule, FindingSeverity, SlideFinding } from '../api/types';

// Every rule in the order of the C# FindingRule enum (the server's sort order within a severity; the C# order is the source of truth).
export const RULE_ORDER: readonly FindingRule[] = [
  'overflow',
  'fit',
  'imageNotLoaded',
  'renderFailed',
  'smallText',
  'lowContrast',
  'imageLowResolution',
  'genericAlt',
  'layoutOutsideTemplate',
  'textOnImage',
];

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

// A finding as a measured rule builds it: measureSlide adds the language (always null in the browser).
export type MeasuredFinding = Omit<SlideFinding, 'language'>;

// One measured rule: reads a rendered slide frame, returns its findings.
export type MeasureRule = (frame: HTMLElement, slide: number, canvas: Canvas) => MeasuredFinding[];

import type { FindingRule, FindingSeverity } from '../api/types';

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

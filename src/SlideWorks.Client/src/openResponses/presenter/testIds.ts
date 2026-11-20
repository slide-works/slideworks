// Presenter open-response controls (SW-596). The panel sits in the presenter window; the strip sits on the phone remote.
export const OPEN_PRESENTER_IDS = {
  panel: 'open-response-panel',
  strip: 'open-response-strip',
  phase: 'open-response-phase',
  guests: 'open-response-guests',
  open: 'open-response-open',
  close: 'open-response-close',
  newRound: 'open-response-new-round',
  error: 'open-response-control-error',
  entry: (number: number) => `open-response-entry-${number}`,
  hide: (number: number) => `open-response-hide-${number}`,
  restore: (number: number) => `open-response-restore-${number}`,
} as const;

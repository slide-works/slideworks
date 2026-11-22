// The data-testid of the layout review page's parts, so specs never spell one by hand.
export type ReviewRender = 'sample' | 'dark' | 'steps' | 'maximum';

export const REVIEW_TEST_IDS = {
  status: 'layout-review-status',
  approve: 'layout-review-approve',
  approveReason: 'layout-review-approve-reason',
  checks: 'layout-review-checks',
  gradientNote: 'layout-review-gradient-note',
  safeAreaBox: 'layout-review-safe-area',
  error: 'layout-review-error',
} as const;

export const reviewRenderTestId = (render: ReviewRender) => `layout-review-render-${render}`;

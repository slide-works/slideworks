// Test ids for the Talks (the signed-in person's decks) screen. Specs assert these, never copy.
export const talksTestIds = {
  root: 'talks',
  loading: 'talks-loading',
  list: 'talks-list',
  row: (id: string) => `talks-row-${id}`,
  empty: 'talks-empty',
  failed: 'talks-failed',
  retry: 'talks-retry',
} as const;

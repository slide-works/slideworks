// The guest pages' test ids (SW-5 J5). The acceptance page objects (Pages/GuestPage.cs, Pages/JoinCodePage.cs) mirror
// every one of them; add an id here and there together.
export const GUEST_TEST_IDS = {
  page: 'guest-page',
  loading: 'guest-loading',
  deckTitle: 'guest-deck-title',
  presenter: 'guest-presenter',
  joined: 'guest-joined',
  following: 'guest-following',
  slide: 'guest-slide',
  progress: 'guest-progress',
  activity: 'guest-activity',
  activityWaiting: 'guest-activity-waiting',
  lobby: 'guest-lobby',
  ended: 'guest-ended',
  endedSlides: 'guest-ended-slides',
  offline: 'guest-offline',
  offlineRetry: 'guest-offline-retry',
  reconnecting: 'guest-reconnecting',
  privacyOpen: 'guest-privacy-open',
  privacySheet: 'guest-privacy-sheet',
  privacyClose: 'guest-privacy-close',
} as const;

export const JOIN_CODE_TEST_IDS = {
  page: 'join-code-page',
  // The six digit boxes are join-code-box-1 to join-code-box-6.
  error: 'join-code-error',
} as const;

export const JOIN_CODE_LENGTH = 6;

export const joinCodeBoxTestId = (position: number) => `join-code-box-${position}`;

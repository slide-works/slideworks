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
  needsCookies: 'guest-needs-cookies',
  needsCookiesRetry: 'guest-needs-cookies-retry',
  error: 'guest-error',
  errorRetry: 'guest-error-retry',
  chip: 'guest-chip',
  waitingForPresenter: 'guest-waiting-for-presenter',
  offlineRetry: 'guest-offline-retry',
  reconnecting: 'guest-reconnecting',
  privacyOpen: 'guest-privacy-open',
  privacySheet: 'guest-privacy-sheet',
  privacyClose: 'guest-privacy-close',
  // SW-883: the language chip, the sheet it opens and each language in it, the one-time "Showing the slides in" banner
  // and its Change button, the "lines on this slide are in English" note and the failed-switch message.
  languageChip: 'language-chip',
  languageSheet: 'language-sheet',
  languageOption: 'language-option',
  languageBanner: 'language-banner',
  languageBannerChange: 'language-banner-change',
  fallbackNote: 'fallback-note',
  languageError: 'language-error',
  // SW-883 landscape: the tap overlay, its Full screen button (Android) and that button's error, and the one-time
  // portrait hint with its close button.
  landscapeOverlay: 'landscape-overlay',
  landscapeFullscreen: 'landscape-fullscreen',
  landscapeFullscreenError: 'landscape-fullscreen-error',
  portraitHint: 'portrait-hint',
  portraitHintClose: 'portrait-hint-close',
} as const;

export const JOIN_CODE_TEST_IDS = {
  page: 'join-code-page',
  // The six digit boxes are join-code-box-1 to join-code-box-6.
  error: 'join-code-error',
} as const;

export const JOIN_CODE_LENGTH = 6;

export const joinCodeBoxTestId = (position: number) => `join-code-box-${position}`;

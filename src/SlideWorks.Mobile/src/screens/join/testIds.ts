// Test ids for the Join (guest join and live slide number) screen. Specs assert these, never copy.
export const joinTestIds = {
  root: 'join',
  server: 'join-server',
  joining: 'join-joining',
  title: 'join-title',
  slide: 'join-slide',
  lobby: 'join-lobby',
  ended: 'join-ended',
  offline: 'join-offline',
  noSession: 'join-no-session',
  tooManyAttempts: 'join-too-many-attempts',
} as const;

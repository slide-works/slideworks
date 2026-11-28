// Test ids for the Account (name, email, server, version, Sign out) screen. Specs assert these, never copy.
export const accountTestIds = {
  root: 'account',
  name: 'account-name',
  email: 'account-email',
  server: 'account-server',
  version: 'account-version',
  pending: 'account-pending',
  signOut: 'account-sign-out',
  confirmSignOut: 'account-confirm-sign-out',
  cancelSignOut: 'account-cancel-sign-out',
} as const;

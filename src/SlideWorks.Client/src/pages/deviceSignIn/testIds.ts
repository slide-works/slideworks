// The app sign-in confirm page's test ids (/app/sign-in, contract K6). The acceptance page object
// (Pages/DeviceSignInPage.cs) mirrors every one of them; add an id here and there together.
export const DEVICE_SIGN_IN_TEST_IDS = {
  page: 'device-sign-in-page',
  heading: 'device-sign-in-heading',
  email: 'device-sign-in-email',
  warning: 'device-sign-in-warning',
  continueButton: 'device-sign-in-continue',
  cancel: 'device-sign-in-cancel',
  openApp: 'device-sign-in-open-app',
  incomplete: 'device-sign-in-incomplete',
  refused: 'device-sign-in-refused',
  failed: 'device-sign-in-failed',
  unreachable: 'device-sign-in-unreachable',
} as const;

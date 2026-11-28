/* eslint-disable @typescript-eslint/no-require-imports -- jest.mock factories are hoisted above the imports, so they load modules with require(). */
// Jest setup for screen specs (contract K10): every native module the app uses gets a fake here, so specs run without a
// device. Storage is in memory and reset before each test; renderApp seeds it.
import { mockStorage } from './src/test-support/nativeMocks';
import { restoreFetch } from './src/test-support/fakeApi';
import { resetFakeHub } from './src/test-support/fakeHub';

jest.mock('expo-secure-store', () => require('./src/test-support/nativeMocks').secureStoreModule);
jest.mock('expo-sqlite/kv-store', () => require('./src/test-support/nativeMocks').kvStoreModule);
jest.mock('expo-web-browser', () => require('./src/test-support/nativeMocks').webBrowserModule);
jest.mock('@react-native-community/netinfo', () =>
  require('@react-native-community/netinfo/jest/netinfo-mock.js'),
);
jest.mock(
  'react-native-safe-area-context',
  () => require('react-native-safe-area-context/jest/mock').default,
);
jest.mock('react-native/Libraries/Utilities/useColorScheme', () => ({
  __esModule: true,
  default: jest.fn(() => 'light'),
}));
jest.mock('expo-constants', () => ({
  __esModule: true,
  default: {
    expoConfig: { name: 'SlideWorks', slug: 'slideworks', scheme: 'slideworks', version: '0.0.0' },
  },
}));
jest.mock('expo-application', () => {
  const { APP_BUILD, APP_VERSION } = require('./src/test-support/nativeMocks');
  return {
    nativeApplicationVersion: APP_VERSION,
    nativeBuildVersion: APP_BUILD,
    applicationId: 'app.slideworks.mobile',
    applicationName: 'SlideWorks',
  };
});
jest.mock('expo-device', () => {
  const { DEVICE_NAME } = require('./src/test-support/nativeMocks');
  return { deviceName: DEVICE_NAME, modelName: 'Pixel 9', osName: 'Android', isDevice: false };
});
jest.mock('@sentry/react-native', () => ({
  init: jest.fn(),
  wrap: (component: unknown) => component,
  captureException: jest.fn(),
  ErrorBoundary: ({ children }: { children: unknown }) => children,
}));
// Screens build hubs through createHubConnection; fakeHub() decides what the next one does.
jest.mock('@slideworks/core/realtime/hub', () => ({
  ...jest.requireActual('@slideworks/core/realtime/hub'),
  createHubConnection: (...args: unknown[]) =>
    require('./src/test-support/fakeHub').fakeHubFactory(...args),
}));

beforeEach(() => {
  mockStorage.reset();
  resetFakeHub();
});

afterEach(() => {
  restoreFetch();
});

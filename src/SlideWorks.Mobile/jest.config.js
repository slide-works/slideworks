// Screens: jest-expo with React Native Testing Library, *.spec.tsx only (pure modules are *.test.ts under Vitest).
module.exports = {
  preset: 'jest-expo',
  testMatch: ['<rootDir>/src/**/*.spec.tsx'],
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  // Busy CI runners blow Jest's 5 s default.
  // The preset transforms @sentry/react-native only; its @sentry/browser and @sentry/core dependencies ship ESM.
  transformIgnorePatterns: [
    '/node_modules/(?!(.pnpm|react-native|@react-native|@react-native-community|expo|@expo|@expo-google-fonts|react-navigation|@react-navigation|@sentry|native-base|standard-navigation))',
    '/node_modules/react-native-reanimated/plugin/',
    '/node_modules/@react-native/babel-preset/',
  ],
  testTimeout: 15000,
};

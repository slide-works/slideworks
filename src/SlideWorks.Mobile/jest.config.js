// Screens: jest-expo with React Native Testing Library, *.spec.tsx only (pure modules are *.test.ts under Vitest).
module.exports = {
  preset: 'jest-expo',
  testMatch: ['<rootDir>/src/**/*.spec.tsx'],
  setupFilesAfterEnv: ['<rootDir>/jest.setup.ts'],
  // Busy CI runners blow Jest's 5 s default.
  testTimeout: 15000,
};

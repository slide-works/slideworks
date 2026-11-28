const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');

// The test harness (src/test-support/) is for tests only: production code never imports it.
const testFiles = ['jest.setup.ts', '**/*.test.ts', '**/*.spec.tsx', 'src/test-support/**'];
const testSupportImports = ['**/test-support', '**/test-support/**'];

module.exports = tseslint.config(
  // fixtures/ holds code that breaks these rules on purpose (src/lint.test.ts lints it).
  { ignores: ['android', 'ios', 'dist', '.expo', 'expo-env.d.ts', 'fixtures'] },
  eslint.configs.recommended,
  tseslint.configs.recommended,
  {
    // The CommonJS config files Metro, Babel, Jest and ESLint load.
    files: ['*.js'],
    languageOptions: {
      sourceType: 'commonjs',
      globals: { module: 'writable', require: 'readonly', __dirname: 'readonly' },
    },
    rules: { '@typescript-eslint/no-require-imports': 'off' },
  },
  {
    files: ['scripts/*.mjs'],
    languageOptions: { globals: { URL: 'readonly' } },
  },
  {
    ignores: testFiles,
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: testSupportImports,
              message: 'Only test files (*.test.ts, *.spec.tsx) import src/test-support/.',
            },
          ],
        },
      ],
    },
  },
);

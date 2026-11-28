import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';

// The core runs in the SPA (a browser) and in the app (React Native), so it touches neither host directly: storage,
// connectivity, origin and credentials come through the platform seam, and UI code stays in each host.
export const bannedGlobals = ['window', 'document', 'localStorage', 'sessionStorage', 'navigator'];
export const bannedImports = [
  'react',
  'react/*',
  'react-dom',
  'react-dom/*',
  'react-native',
  'react-native/*',
  '**/SlideWorks.Client',
  '**/SlideWorks.Client/**',
];

export default tseslint.config(
  // fixtures/ holds code that breaks these rules on purpose (src/core.test.ts lints it).
  { ignores: ['fixtures'] },
  eslint.configs.recommended,
  tseslint.configs.recommended,
  {
    rules: {
      'no-restricted-globals': [
        'error',
        ...bannedGlobals.map((name) => ({
          name,
          message: `${name} is banned in @slideworks/core: ask the host through platform().`,
        })),
      ],
      // globalThis.window and self.localStorage reach the same hosts without naming a global.
      'no-restricted-properties': [
        'error',
        ...['globalThis', 'self'].flatMap((object) =>
          bannedGlobals.map((property) => ({
            object,
            property,
            message: `${object}.${property} is banned in @slideworks/core: ask the host through platform().`,
          })),
        ),
      ],
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: bannedImports,
              message:
                '@slideworks/core is pure TypeScript: no React, React DOM, React Native or SPA imports.',
            },
          ],
        },
      ],
    },
  },
);

import eslint from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      '../SlideWorks.WebApi/wwwroot',
      'playwright/.cache',
      'playwright-report',
      'test-results',
    ],
  },
  eslint.configs.recommended,
  tseslint.configs.recommended,
  // eslint-plugin-react-hooks@7's top-level `recommended-latest` still
  // exports `plugins` as a legacy string array, which ESLint 10's flat
  // config rejects; the properly-shaped flat variant lives under `.flat`.
  reactHooks.configs.flat['recommended-latest'],
  {
    rules: {
      // Slot text is plain text (D12): render it as React text nodes. Never
      // inject HTML, and never parse Markdown or HTML out of slot text.
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
          message:
            'dangerouslySetInnerHTML is banned: render text as plain React text nodes (slot text is never HTML).',
        },
      ],
    },
  },
);

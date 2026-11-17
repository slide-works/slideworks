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
  // Keep this block LAST. A later block that sets no-restricted-syntax replaces
  // this whole list rather than adding to it, so append new selectors here.
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
        {
          // A props object for a spread (<p {...props} />) or React.createElement.
          selector:
            "Property[key.name='dangerouslySetInnerHTML'], Property[key.value='dangerouslySetInnerHTML']",
          message:
            'dangerouslySetInnerHTML is banned, in props objects too: render text as plain React text nodes.',
        },
        {
          selector: 'AssignmentExpression[left.property.name=/^(innerHTML|outerHTML)$/]',
          message:
            'Assigning innerHTML or outerHTML is banned: render text as plain React text nodes.',
        },
        {
          selector: "CallExpression[callee.property.name='insertAdjacentHTML']",
          message: 'insertAdjacentHTML is banned: render text as plain React text nodes.',
        },
        {
          // No Date in the SPA (D7): times come from the server, advanced with performance.now() deltas.
          selector: "NewExpression[callee.name='Date']",
          message:
            'new Date() is banned in the SPA: use server times, performance.now() deltas and presentation/format.ts. See .claude/skills/no-browser-datetime/.',
        },
        {
          selector: "MemberExpression[object.name='Date']",
          message:
            'Date.now(), Date.parse() and the rest of Date are banned in the SPA: use server times, performance.now() deltas and presentation/format.ts. See .claude/skills/no-browser-datetime/.',
        },
      ],
    },
  },
);

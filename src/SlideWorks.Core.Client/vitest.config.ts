import { defineConfig } from 'vitest/config';

// Pure TypeScript: the core's tests run in Node, with no DOM, so a browser global the lint rules miss still fails.
export default defineConfig({
  test: { environment: 'node', include: ['src/**/*.test.ts'] },
});

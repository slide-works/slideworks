import { defineConfig } from 'vitest/config';

// Pure modules: *.test.ts in Node, with no React Native and nothing native (screens are *.spec.tsx under Jest).
export default defineConfig({
    test: { environment: 'node', include: ['src/**/*.test.ts'] },
});

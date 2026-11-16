/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// The shared contracts (screen JSON, sample deck) at the repo's src/contracts,
// reached as @contracts/* (the same alias as tsconfig.json's paths).
export const contractsDir = decodeURIComponent(new URL('../contracts', import.meta.url).pathname);

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@contracts': contractsDir } },
  server: {
    // The dev server may serve the contracts, which sit outside this folder.
    fs: { allow: ['.', contractsDir] },
    port: 3000,
    // The proxy target below must never silently shift ports.
    strictPort: true,
    proxy: {
      // Kestrel's port, from src/SlideWorks.WebApi/Properties/launchSettings.json.
      '/api': { target: 'http://localhost:5510', changeOrigin: true },
      // The realtime hubs (DeckHub), over WebSockets.
      '/hubs': { target: 'http://localhost:5510', changeOrigin: true, ws: true },
    },
  },
  build: {
    // Straight into the API project: ASP.NET serves what Vite emits, no copy step.
    outDir: '../SlideWorks.WebApi/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    // *.spec.tsx are Playwright component tests (pnpm run test-ct), not vitest.
    include: ['src/**/*.test.{ts,tsx}'],
  },
});

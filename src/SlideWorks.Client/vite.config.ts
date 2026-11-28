/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// The shared contracts (screen JSON, sample deck) at the repo's src/contracts,
// reached as @contracts/* (the same alias as tsconfig.json's paths).
export const contractsDir = decodeURIComponent(new URL('../contracts', import.meta.url).pathname);
// @slideworks/core, the workspace package the SPA imports its API layer from. It exports its .ts sources, so the dev
// server reads them from that folder.
export const coreDir = decodeURIComponent(
  new URL('../SlideWorks.Core.Client', import.meta.url).pathname,
);

// Kestrel's port, from src/SlideWorks.WebApi/Properties/launchSettings.json.
const API_PROXY = { target: 'http://localhost:5510', changeOrigin: true };

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@contracts': contractsDir } },
  server: {
    // The dev server may serve the contracts and the core, which sit outside this folder.
    fs: { allow: ['.', contractsDir, coreDir] },
    port: 3000,
    // The proxy target below must never silently shift ports.
    strictPort: true,
    // Everything the server answers goes through this origin, so the browser only ever talks to :3000. In Development
    // SLIDEWORKS_PUBLIC_URL is http://localhost:3000 (appsettings.Development.json), so the server takes this as its
    // own origin: the cross-site write guard accepts the browser's Origin, and sign-in and OAuth callbacks come back
    // here. changeOrigin only rewrites the Host header Kestrel sees; the server ignores it in favour of the public URL.
    proxy: {
      '/api': API_PROXY,
      // The realtime hubs (DeckHub), over WebSockets.
      '/hubs': { ...API_PROXY, ws: true },
      // Agents: the MCP endpoint, the OAuth server and its discovery documents. Only the server's OAuth routes are
      // proxied: /oauth/consent is the SPA's consent page, which Vite serves.
      '/mcp': API_PROXY,
      '^/oauth/(register|authorize|token|revoke)(/|\\?|$)': API_PROXY,
      '/.well-known': API_PROXY,
      // Server-side sign-in: /signin/<provider> starts it and /signin-google is the provider's callback. /signin itself
      // is the SPA's sign-in page, which Vite serves, so only paths below it are proxied.
      '^/signin/': API_PROXY,
      '/signin-google': API_PROXY,
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

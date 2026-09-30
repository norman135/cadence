/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url';
import babel from '@rolldown/plugin-babel';
import tailwindcss from '@tailwindcss/vite';
import react, { reactCompilerPreset } from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// The API origin the dev server proxies to. Aspire injects it through service discovery;
// otherwise the API is expected on its default development port.
const apiTarget =
  process.env.services__api__http__0 ?? process.env.CADENCE_API_URL ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [
    react(),
    // React Compiler memoizes components and hooks automatically, so re-renders stay cheap
    // without hand-written useMemo/useCallback.
    babel({ presets: [reactCompilerPreset()] }),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: Number(process.env.PORT ?? 5173),
    // Same-origin requests in development, exactly like production where the API serves the SPA.
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
      '/hubs': { target: apiTarget, ws: true },
    },
  },
  build: {
    // The manifest maps entry and lazy chunks; scripts/check-bundle-budget.mjs reads it.
    manifest: true,
  },
  test: {
    // Worker threads start faster than child processes, and are reliable on Windows.
    pool: 'threads',
    // Playwright owns e2e/.
    include: ['src/**/*.test.{ts,tsx}'],
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    restoreMocks: true,
  },
});

/// <reference types="vitest/config" />
import { defineConfig, loadEnv } from 'vite';
import react, { reactCompilerPreset } from '@vitejs/plugin-react';
import babel from '@rolldown/plugin-babel';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';

export default defineConfig(({ mode }) => {
  const proxyTarget = loadEnv(mode, import.meta.dirname, '').API_PROXY_TARGET;

  return {
    plugins: [
      tanstackRouter({ target: 'react', autoCodeSplitting: true }),
      ...(mode === 'test' ? [] : [babel({ presets: [reactCompilerPreset()] })]),
      react(),
      tailwindcss(),
    ],
    resolve: { tsconfigPaths: true },
    build: { manifest: true },
    server: {
      ...(proxyTarget ? { proxy: { '/api': { target: proxyTarget, changeOrigin: true } } } : {}),
    },
    test: {
      projects: [
        { extends: true, test: { name: 'unit', environment: 'node', include: ['scripts/**/*.test.ts'] } },
        {
          extends: true,
          test: {
            name: 'dom',
            environment: 'jsdom',
            setupFiles: ['./src/test/setup.ts'],
            // Heavy editor/page suites exceed 5 s under v8 coverage on busy runners (#148, #179).
            testTimeout: 15_000,
            include: ['src/**/*.test.{ts,tsx}'],
          },
        },
      ],
      coverage: {
        provider: 'v8',
        include: ['src/**', 'scripts/**'],
        exclude: [
          'src/shared/api/generated/**',
          'src/routeTree.gen.ts',
          'src/test/**',
          'src/main.tsx',
          '**/*.test.{ts,tsx}',
          '**/*.d.ts',
        ],
        thresholds: { 'src/features/**': { lines: 80, branches: 70 } },
      },
    },
  };
});

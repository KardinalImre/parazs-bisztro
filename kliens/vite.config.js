import { defineConfig } from 'vite';

export default defineConfig({
  publicDir: 'nyilvanos',
  build: { outDir: '../szerver/wwwroot', emptyOutDir: true },
  esbuild: { jsx: 'automatic' },
  server: { proxy: { '/api': 'http://127.0.0.1:5080' } },
});

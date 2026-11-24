import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// The API Gateway has no CORS configuration, so the dev server proxies /api to it.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const gateway = env.VITE_GATEWAY_URL ?? 'http://localhost:9090';
  return {
    plugins: [react()],
    resolve: { alias: { '@': new URL('./src', import.meta.url).pathname } },
    build: {
      rolldownOptions: {
        output: {
          // Long-lived vendor chunks: UI kit and charts change less often than app code.
          advancedChunks: {
            groups: [
              { name: 'charts', test: /node_modules[\\/](recharts|d3-|victory-vendor|@mantine[\\/]charts)/ },
              { name: 'mantine', test: /node_modules[\\/]@mantine/ },
              { name: 'vendor', test: /node_modules[\\/](react|react-dom|react-router|@tanstack|scheduler)/ },
            ],
          },
        },
      },
    },
    server: { port: 5173, proxy: { '/api': { target: gateway, changeOrigin: true } } },
    preview: { port: 4173, proxy: { '/api': { target: gateway, changeOrigin: true } } },
  };
});

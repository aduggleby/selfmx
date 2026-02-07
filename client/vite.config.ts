import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

const isTest = process.env.PLAYWRIGHT_TEST === 'true'

export default defineConfig(({ command }) => ({
  // The admin UI is hosted by the backend under /ui in production.
  // Keep dev server at / for convenience (and to avoid changing Playwright tests).
  base: command === 'build' ? '/ui/' : '/',
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 17401,
    // Disable proxy during Playwright tests - requests are mocked via page.route()
    proxy: isTest ? undefined : {
      '/admin': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
      '/api-keys': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
      '/audit': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
      '/domains': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
      '/emails': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
      '/sent-emails': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
      '/system': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
    },
  },
}))

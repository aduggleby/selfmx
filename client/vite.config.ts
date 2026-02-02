import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

const isTest = process.env.PLAYWRIGHT_TEST === 'true'

export default defineConfig({
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
      '/v1': {
        target: 'http://localhost:17400',
        changeOrigin: true,
      },
    },
  },
})

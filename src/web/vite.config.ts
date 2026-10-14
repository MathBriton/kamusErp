/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// API local: `dotnet run` (5249) por padrão; com Docker Compose use KAMUS_API_URL=http://localhost:8080
const apiUrl = process.env.KAMUS_API_URL ?? 'http://localhost:5249'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': apiUrl,
      '/health': apiUrl,
      '/hubs': { target: apiUrl, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})

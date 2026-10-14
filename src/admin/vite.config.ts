/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// Mesma API do cliente (`dotnet run`, porta 5249); o painel usa só as rotas /admin-api.
const apiUrl = process.env.KAMUS_API_URL ?? 'http://localhost:5249'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5174,
    proxy: { '/admin-api': apiUrl },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})

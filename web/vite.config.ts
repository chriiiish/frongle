import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  server: {
    // npm run dev sends API calls to the API that you run on your machine (see the README).
    proxy: { '/api': 'http://localhost:5162' },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test-setup.ts',
  },
})

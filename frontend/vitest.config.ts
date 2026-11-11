import { defineConfig } from 'vitest/config'

// Entorno node (sin jsdom): el cliente HTTP solo necesita fetch, document.cookie,
// window.location y navigator.locks, que los tests simulan con vi.stubGlobal.
// Config aparte de vite.config.ts para no cargar plugins ni el proxy de /api.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})

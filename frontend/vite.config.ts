import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// /api pasa por el proxy para que SPA y API compartan origen: las cookies
// de sesión (HttpOnly, SameSite) son first-party y no hace falta CORS.
// changeOrigin queda en false para que el backend vea el host de la SPA
// y arme con él la redirect_uri del login federado.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5112',
        changeOrigin: false,
      },
    },
  },
})

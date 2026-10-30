import { http } from './http'
import type { AuthSession } from './http'

// Endpoints de sesión (backend/Features/Auth). Las cookies las pone y
// borra el servidor; aquí solo se lee el perfil que devuelven.

export type ExternalProvider = 'microsoft' | 'google'

export const authApi = {
  login(usernameOrEmail: string, password: string): Promise<AuthSession> {
    return http.post<AuthSession>('/api/auth/login', { usernameOrEmail, password })
  },
  me(): Promise<AuthSession> {
    return http.get<AuthSession>('/api/auth/me')
  },
  logout(): Promise<void> {
    return http.post<void>('/api/auth/logout')
  },
  async providers(): Promise<ExternalProvider[]> {
    const response = await http.get<{ providers: ExternalProvider[] }>('/api/auth/providers')
    return response.providers
  },
  // Navegación completa (no fetch): el proveedor redirige de vuelta al
  // backend, que deja las cookies y regresa a la SPA en returnUrl.
  externalLoginUrl(provider: ExternalProvider, returnUrl: string): string {
    const base = import.meta.env.VITE_API_BASE_URL ?? ''
    return `${base}/api/auth/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`
  },
}

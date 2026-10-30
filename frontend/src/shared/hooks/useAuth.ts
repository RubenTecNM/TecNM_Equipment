import { createContext, useContext } from 'react'
import type { AuthSession } from '../api/http'

export interface AuthContextValue {
  session: AuthSession | null
  // true mientras se consulta GET /api/auth/me al arrancar: la cookie
  // HttpOnly no se puede leer desde JS, así que hasta entonces no se
  // sabe si hay sesión.
  isLoading: boolean
  login: (usernameOrEmail: string, password: string) => Promise<AuthSession>
  logout: () => Promise<void>
  hasPermission: (permission: string) => boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth debe usarse dentro de <AuthProvider>')
  }
  return context
}

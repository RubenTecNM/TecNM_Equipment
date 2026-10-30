import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { authApi } from '../api/auth'
import { setSessionLostHandler } from '../api/http'
import type { AuthSession } from '../api/http'
import { AuthContext } from './useAuth'

// Archivo aparte del hook useAuth (useAuth.ts):
// react-refresh exige que un archivo con un
// componente no exporte también hooks/funciones.
//
// La sesión vive en cookies HttpOnly; aquí solo se guarda en memoria
// el perfil (con permisos) que devuelve el servidor.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    setSessionLostHandler(() => setSession(null))

    let cancelled = false
    authApi
      .me()
      .then((profile) => {
        if (!cancelled) setSession(profile)
      })
      .catch(() => {
        if (!cancelled) setSession(null)
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false)
      })

    return () => {
      cancelled = true
      setSessionLostHandler(null)
    }
  }, [])

  const login = useCallback(async (usernameOrEmail: string, password: string) => {
    const profile = await authApi.login(usernameOrEmail, password)
    setSession(profile)
    return profile
  }, [])

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      setSession(null)
    }
  }, [])

  const hasPermission = useCallback(
    (permission: string) => session?.permissions.includes(permission) ?? false,
    [session],
  )

  const value = useMemo(
    () => ({ session, isLoading, login, logout, hasPermission }),
    [session, isLoading, login, logout, hasPermission],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

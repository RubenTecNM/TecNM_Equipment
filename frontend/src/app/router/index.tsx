import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { AuthProvider } from '../../shared/hooks/AuthProvider'
import { useAuth } from '../../shared/hooks/useAuth'
import { AuthLayout } from '../layouts/AuthLayout'
import { MainLayout } from '../layouts/MainLayout'
import { LoginPage } from '../../modules/auth/pages/Login'
import { DashboardPage } from '../../modules/dashboard/pages/DashboardPage'
import { AssetsPage } from '../../modules/assets/pages/AssetsPage'
import { AssetDetailPage } from '../../modules/assets/pages/AssetDetailPage'
import { LocationsPage } from '../../modules/locations/pages/LocationsPage'
import { LocationDetailPage } from '../../modules/locations/pages/LocationDetailPage'
import { IncidentsPage } from '../../modules/incidents/pages/IncidentsPage'
import { IncidentDetailPage } from '../../modules/incidents/pages/IncidentDetailPage'

function RequireAuth() {
  const { session, isLoading } = useAuth()
  const location = useLocation()

  // La cookie HttpOnly no se lee desde JS: hasta que GET /api/auth/me
  // responda no se sabe si hay sesión, así que no se redirige aún.
  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background" role="status">
        <p className="text-sm text-muted-foreground">Cargando sesión…</p>
      </div>
    )
  }

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return (
    <MainLayout>
      <Outlet />
    </MainLayout>
  )
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route
            path="/login"
            element={
              <AuthLayout>
                <LoginPage />
              </AuthLayout>
            }
          />
          <Route element={<RequireAuth />}>
            <Route index element={<DashboardPage />} />
            <Route path="assets" element={<AssetsPage />} />
            <Route path="assets/:assetId" element={<AssetDetailPage />} />
            <Route path="locations" element={<LocationsPage />} />
            <Route
              path="locations/:locationId"
              element={<LocationDetailPage />}
            />
            <Route path="incidents" element={<IncidentsPage />} />
            <Route
              path="incidents/:incidentId"
              element={<IncidentDetailPage />}
            />
            {/* Rutas de módulos se agregan dentro de RequireAuth (FASE 2-3) */}
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}

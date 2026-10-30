-- ============================================================
-- PERMISO: procurement.manage
-- Cubre CRUD de suppliers, acquisitions y asset_warranties.
-- Idempotente: seguro de re-ejecutar.
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('procurement.manage', 'Administrar adquisiciones', 'Crear y consultar proveedores, adquisiciones y garantías de activos')
ON CONFLICT (code) DO NOTHING;

-- Por ahora solo ADMIN lo tiene.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r, permissions p
WHERE r.code = 'ADMIN' AND p.code = 'procurement.manage'
ON CONFLICT DO NOTHING;

-- ============================================================
-- FIN
-- ============================================================

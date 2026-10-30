-- ============================================================
-- Permiso users.view (selector de usuarios en asignaciones)
-- Idempotente: seguro de re-ejecutar
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('users.view', 'Ver usuarios', 'Consultar usuarios activos (selector de asignación)')
ON CONFLICT (code) DO NOTHING;

-- ADMIN: todos los permisos (CROSS JOIN del seed base lo cubre;
-- se re-afirma aquí por si el permiso se agregó después)
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'ADMIN' AND p.code = 'users.view'
ON CONFLICT DO NOTHING;

-- TECNICO y RESPONSABLE_AREA asignan activos: necesitan el selector
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('TECNICO', 'RESPONSABLE_AREA')
  AND p.code = 'users.view'
ON CONFLICT DO NOTHING;

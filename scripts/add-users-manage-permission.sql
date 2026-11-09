-- ============================================================
-- Permiso users.manage (alta de usuarios: POST /api/auth/register)
-- Sustituye la comprobación por rol ([Authorize(Roles = "ADMIN")]).
-- Idempotente: seguro de re-ejecutar
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('users.manage', 'Gestionar usuarios', 'Dar de alta cuentas de usuario')
ON CONFLICT (code) DO NOTHING;

-- ADMIN conserva la capacidad que hoy tiene por rol.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'ADMIN' AND p.code = 'users.manage'
ON CONFLICT DO NOTHING;

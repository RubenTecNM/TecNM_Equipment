-- ============================================================
-- PERMISOS: módulo Incidents
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING)
-- incidents.create e incidents.resolve ya existen (02_seed_permissions).
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('incidents.view',   'Ver incidentes',     'Consultar el listado y detalle de incidentes'),
  ('incidents.delete', 'Eliminar incidentes', 'Baja lógica de incidentes registrados por error')
ON CONFLICT (code) DO NOTHING;

-- ADMIN: recibe ambos permisos nuevos
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'ADMIN'
  AND p.code IN ('incidents.view', 'incidents.delete')
ON CONFLICT DO NOTHING;

-- TECNICO y RESPONSABLE_AREA: solo consulta
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('TECNICO', 'RESPONSABLE_AREA')
  AND p.code = 'incidents.view'
ON CONFLICT DO NOTHING;

-- Usuarios de prueba para validar RBAC en el frontend (RESPONSABLE_AREA y USUARIO_GENERAL).
-- Idempotente. El hash NO vive en el repo: se pasa por variable de psql.
-- Uso: psql ... -v ON_ERROR_STOP=1 -v hash="$HASH" -f scripts/seed-test-users.sql
-- (hash generado con backend/tools/PasswordHasher)
\if :{?hash}
\else
  \echo 'Falta -v hash=...'
  \quit
\endif
SET search_path TO inventario, public;
INSERT INTO app_users (username, email, password_hash, first_name, last_name, role_id, status, version)
SELECT v.username, v.email, :'hash', 'Prueba', v.last_name, r.role_id, 'activo', 1
FROM (VALUES
  ('test_responsable', 'test-responsable@localhost', 'Responsable Area', 'RESPONSABLE_AREA'),
  ('test_usuario',     'test-usuario@localhost',     'Usuario General',  'USUARIO_GENERAL')
) AS v(username, email, last_name, role_code)
JOIN roles r ON r.code = v.role_code
ON CONFLICT (username) DO NOTHING;
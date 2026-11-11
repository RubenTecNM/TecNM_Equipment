-- ============================================================
-- Unicidad de username y email sin distinguir mayúsculas (Lote 5.4)
-- Complementa (no sustituye) los UNIQUE de database.sql: esos distinguen
-- mayúsculas, así que "Ana" y "ana" podían coexistir.
-- Idempotente: seguro de re-ejecutar.
--
-- ANTES de aplicarlo en una BD con datos, comprobar que no hay duplicados
-- (si los hubiera, CREATE UNIQUE INDEX falla y no cambia nada):
--   SELECT lower(username), count(*) FROM inventario.app_users GROUP BY 1 HAVING count(*) > 1;
--   SELECT lower(email),    count(*) FROM inventario.app_users GROUP BY 1 HAVING count(*) > 1;
-- ============================================================

CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_username_lower
    ON inventario.app_users (lower(username));

CREATE UNIQUE INDEX IF NOT EXISTS ux_app_users_email_lower
    ON inventario.app_users (lower(email));

-- ============================================================
-- Mantenimiento (M.1): flujo programar -> iniciar -> cerrar/cancelar
-- Amplía inventario.maintenance_records con estado, inicio, vínculo al
-- incidente, costo, estado del activo antes/después y cancelación.
--
-- Idempotente: seguro de re-ejecutar. TODO va en UN bloque DO (una sola
-- sentencia = atómica): o se aplica entero o no cambia nada.
--
-- Orden: columnas -> relleno de filas antiguas -> FK y CHECK -> índices.
--
-- El RELLENO solo se hace cuando este script AÑADE la columna status (primera
-- ejecución). Una ejecución posterior NO recalcula estados: un EN_CURSO (que
-- tiene performed_at NULL) volvería a PROGRAMADO.
--
-- Decisiones del relleno de filas antiguas:
--   * performed_at IS NOT NULL            -> COMPLETADO.
--       performed_at ES la fecha de cierre (no hay completed_at). Como no se
--       conoce el inicio real, started_at = performed_at (lo exige el CHECK
--       de COMPLETADO y la regla performed_at >= started_at).
--   * performed_at IS NULL                -> PROGRAMADO (valor por defecto).
--   * sin scheduled_at ni performed_at    -> scheduled_at = created_at y
--       PROGRAMADO (lo exige el CHECK de PROGRAMADO). Una fila con
--       performed_at pero sin scheduled_at conserva scheduled_at NULL.
--   * Las filas borradas lógicamente se rellenan igual: los CHECK no
--       distinguen borradas.
--   * El relleno actualiza filas existentes: los triggers de versión
--       incrementan version y updated_at de esas filas (no se audita en
--       audit_log: es una migración de datos derivados).
-- ============================================================

DO $$
DECLARE
  v_fresh boolean;
  c record;
BEGIN
  -- ¿Es la primera ejecución? (la columna status aún no existe)
  v_fresh := NOT EXISTS (
    SELECT 1
    FROM pg_attribute
    WHERE attrelid = 'inventario.maintenance_records'::regclass
      AND attname = 'status'
      AND NOT attisdropped
  );

  -- 1. COLUMNAS. status se añade NOT NULL con DEFAULT 'PROGRAMADO' para poder
  --    rellenar; el default se conserva (un registro sin estado es programado).
  ALTER TABLE inventario.maintenance_records
    ADD COLUMN IF NOT EXISTS status              varchar(20) NOT NULL DEFAULT 'PROGRAMADO',
    ADD COLUMN IF NOT EXISTS started_at          timestamptz,
    ADD COLUMN IF NOT EXISTS incident_id         bigint,
    ADD COLUMN IF NOT EXISTS cost                numeric(14,2),
    ADD COLUMN IF NOT EXISTS status_before_id    bigint,
    ADD COLUMN IF NOT EXISTS status_after_id     bigint,
    ADD COLUMN IF NOT EXISTS cancelled_at        timestamptz,
    ADD COLUMN IF NOT EXISTS cancellation_reason varchar(500);

  -- 2. RELLENO de filas antiguas (solo en la primera ejecución).
  IF v_fresh THEN
    UPDATE inventario.maintenance_records
       SET scheduled_at = created_at
     WHERE scheduled_at IS NULL
       AND performed_at IS NULL;

    UPDATE inventario.maintenance_records
       SET status = 'COMPLETADO',
           started_at = performed_at
     WHERE performed_at IS NOT NULL;
  END IF;

  -- 3. FOREIGN KEYS (nulables: no cambian el guardián de relaciones requeridas).
  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                 WHERE conrelid = 'inventario.maintenance_records'::regclass
                   AND conname = 'fk_maintenance_records_incident') THEN
    ALTER TABLE inventario.maintenance_records
      ADD CONSTRAINT fk_maintenance_records_incident
      FOREIGN KEY (incident_id) REFERENCES inventario.incidents (incident_id);
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                 WHERE conrelid = 'inventario.maintenance_records'::regclass
                   AND conname = 'fk_maintenance_records_status_before') THEN
    ALTER TABLE inventario.maintenance_records
      ADD CONSTRAINT fk_maintenance_records_status_before
      FOREIGN KEY (status_before_id) REFERENCES inventario.asset_statuses (asset_status_id);
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                 WHERE conrelid = 'inventario.maintenance_records'::regclass
                   AND conname = 'fk_maintenance_records_status_after') THEN
    ALTER TABLE inventario.maintenance_records
      ADD CONSTRAINT fk_maintenance_records_status_after
      FOREIGN KEY (status_after_id) REFERENCES inventario.asset_statuses (asset_status_id);
  END IF;

  -- 4. CHECK (nombres explícitos). La coherencia entre estado y fechas vive en
  --    la BD, no solo en los handlers.
  FOR c IN
    SELECT * FROM (VALUES
      ('ck_maintenance_records_status',
       $chk$status IN ('PROGRAMADO', 'EN_CURSO', 'COMPLETADO', 'CANCELADO')$chk$),
      ('ck_maintenance_records_cost',
       $chk$cost IS NULL OR cost >= 0$chk$),
      ('ck_maintenance_records_programado',
       $chk$status <> 'PROGRAMADO' OR scheduled_at IS NOT NULL$chk$),
      ('ck_maintenance_records_en_curso',
       $chk$status <> 'EN_CURSO' OR started_at IS NOT NULL$chk$),
      ('ck_maintenance_records_completado',
       $chk$status <> 'COMPLETADO' OR (started_at IS NOT NULL AND performed_at IS NOT NULL)$chk$),
      ('ck_maintenance_records_cancelado',
       $chk$status <> 'CANCELADO' OR cancelled_at IS NOT NULL$chk$),
      ('ck_maintenance_records_fechas',
       $chk$started_at IS NULL OR performed_at IS NULL OR performed_at >= started_at$chk$)
    ) AS t(name, expr)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid = 'inventario.maintenance_records'::regclass
                     AND conname = c.name) THEN
      EXECUTE format(
        'ALTER TABLE inventario.maintenance_records ADD CONSTRAINT %I CHECK (%s)',
        c.name, c.expr);
    END IF;
  END LOOP;

  -- 5. ÍNDICES.
  -- Un solo mantenimiento EN_CURSO por activo; uno borrado lógicamente no cuenta.
  CREATE UNIQUE INDEX IF NOT EXISTS ux_maintenance_one_in_progress
    ON inventario.maintenance_records (asset_id)
    WHERE status = 'EN_CURSO' AND NOT is_deleted;

  -- Listados y calendario: por estado y fecha programada.
  CREATE INDEX IF NOT EXISTS ix_maintenance_status_scheduled
    ON inventario.maintenance_records (status, scheduled_at)
    WHERE NOT is_deleted;
END $$;

COMMENT ON COLUMN inventario.maintenance_records.status IS
'PROGRAMADO -> EN_CURSO -> COMPLETADO, o CANCELADO desde PROGRAMADO/EN_CURSO. COMPLETADO y CANCELADO son terminales.';
COMMENT ON COLUMN inventario.maintenance_records.performed_at IS
'Fecha de cierre (COMPLETADO). No existe completed_at.';
COMMENT ON COLUMN inventario.maintenance_records.status_before_id IS
'Estado del activo al iniciar el mantenimiento.';
COMMENT ON COLUMN inventario.maintenance_records.status_after_id IS
'Estado del activo al cerrar o cancelar el mantenimiento.';

-- ============================================================
-- DOCUMENTOS Y EVIDENCIAS (polimórfico, validado por trigger)
-- Idempotente: seguro de re-ejecutar.
-- ============================================================

SET search_path TO inventario, public;

-- ------------------------------------------------------------
-- 1. TABLA DOCUMENTS
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS documents (
  document_id   bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  owner_type    varchar(30) NOT NULL
                CHECK (owner_type IN ('INCIDENT','MAINTENANCE','ASSET_DISPOSAL','ACQUISITION')),
  owner_id      bigint NOT NULL,
  file_name     varchar(255) NOT NULL,
  storage_key   text NOT NULL,
  mime_type     varchar(100) NOT NULL
                CHECK (mime_type IN ('image/jpeg','image/png','application/pdf')),
  size_bytes    bigint NOT NULL CHECK (size_bytes > 0 AND size_bytes <= 5242880),
  description   text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    bigint,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    bigint,
  is_deleted    boolean NOT NULL DEFAULT false,
  deleted_at    timestamptz,
  deleted_by    bigint,
  version       integer NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS ix_documents_owner
ON documents(owner_type, owner_id)
WHERE is_deleted = false;

COMMENT ON TABLE documents IS
'Adjuntos polimórficos (evidencias, actas, facturas). owner_type+owner_id se validan por trigger contra la tabla real.';

-- ------------------------------------------------------------
-- 2. TRIGGER: VALIDAR QUE owner_id EXISTA EN LA TABLA REAL
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION validate_document_owner()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  owner_exists boolean;
BEGIN
  CASE NEW.owner_type
    WHEN 'INCIDENT' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.incidents WHERE incident_id = NEW.owner_id) INTO owner_exists;
    WHEN 'MAINTENANCE' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.maintenance_records WHERE maintenance_record_id = NEW.owner_id) INTO owner_exists;
    WHEN 'ASSET_DISPOSAL' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.asset_disposals WHERE asset_disposal_id = NEW.owner_id) INTO owner_exists;
    WHEN 'ACQUISITION' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.acquisitions WHERE acquisition_id = NEW.owner_id) INTO owner_exists;
    ELSE
      owner_exists := false;
  END CASE;

  IF NOT owner_exists THEN
    RAISE EXCEPTION 'documents.owner_id % no existe en la tabla correspondiente a owner_type=%',
      NEW.owner_id, NEW.owner_type;
  END IF;

  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_documents_validate_owner ON documents;
CREATE TRIGGER trg_documents_validate_owner
BEFORE INSERT OR UPDATE OF owner_type, owner_id ON documents
FOR EACH ROW EXECUTE FUNCTION validate_document_owner();

-- ------------------------------------------------------------
-- 3. SOFT DELETE CONSISTENTE (reusa la función ya existente)
-- ------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_documents_soft_delete ON documents;
CREATE TRIGGER trg_documents_soft_delete
BEFORE UPDATE ON documents
FOR EACH ROW EXECUTE FUNCTION prevent_invalid_soft_delete();

-- ------------------------------------------------------------
-- 4. VERSIONADO (reusa touch_version, igual que assets/locations/users)
-- ------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_documents_version ON documents;
CREATE TRIGGER trg_documents_version
BEFORE UPDATE ON documents
FOR EACH ROW EXECUTE FUNCTION touch_version();

-- ------------------------------------------------------------
-- 5. PERMISO NUEVO: documents.manage (brecha temporal para
--    owner_type sin módulo propio todavía, ej. ACQUISITION)
-- ------------------------------------------------------------
INSERT INTO permissions (code, name, description) VALUES
  ('documents.manage', 'Administrar documentos', 'Subir, ver y eliminar documentos adjuntos')
ON CONFLICT (code) DO NOTHING;

-- Por ahora solo ADMIN lo tiene (Procurement aún no existe como módulo).
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r, permissions p
WHERE r.code = 'ADMIN' AND p.code = 'documents.manage'
ON CONFLICT DO NOTHING;

-- ============================================================
-- FIN
-- ============================================================

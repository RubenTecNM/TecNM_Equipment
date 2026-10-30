using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Data;

public partial class TecnmInventarioDbContext : DbContext
{
    public TecnmInventarioDbContext(DbContextOptions<TecnmInventarioDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Acquisition> Acquisitions { get; set; }

    public virtual DbSet<AcquisitionAsset> AcquisitionAssets { get; set; }

    public virtual DbSet<AppUser> AppUsers { get; set; }

    public virtual DbSet<Asset> Assets { get; set; }

    public virtual DbSet<AssetAssignment> AssetAssignments { get; set; }

    public virtual DbSet<AssetComponent> AssetComponents { get; set; }

    public virtual DbSet<AssetDisposal> AssetDisposals { get; set; }

    public virtual DbSet<AssetMovement> AssetMovements { get; set; }

    public virtual DbSet<AssetOsHistory> AssetOsHistories { get; set; }

    public virtual DbSet<AssetRelation> AssetRelations { get; set; }

    public virtual DbSet<AssetService> AssetServices { get; set; }

    public virtual DbSet<AssetSoftware> AssetSoftwares { get; set; }

    public virtual DbSet<AssetSpecification> AssetSpecifications { get; set; }

    public virtual DbSet<AssetStatus> AssetStatuses { get; set; }

    public virtual DbSet<AssetType> AssetTypes { get; set; }

    public virtual DbSet<AssetWarranty> AssetWarranties { get; set; }

    public virtual DbSet<AssignmentType> AssignmentTypes { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BuildingSetting> BuildingSettings { get; set; }

    public virtual DbSet<ComponentType> ComponentTypes { get; set; }

    public virtual DbSet<DisposalReason> DisposalReasons { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<Incident> Incidents { get; set; }

    public virtual DbSet<IncidentPriority> IncidentPriorities { get; set; }

    public virtual DbSet<IncidentStatus> IncidentStatuses { get; set; }

    public virtual DbSet<IncidentType> IncidentTypes { get; set; }

    public virtual DbSet<InventoryRecord> InventoryRecords { get; set; }

    public virtual DbSet<InventorySession> InventorySessions { get; set; }

    public virtual DbSet<LicenseType> LicenseTypes { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<LocationType> LocationTypes { get; set; }

    public virtual DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }

    public virtual DbSet<MaintenanceType> MaintenanceTypes { get; set; }

    public virtual DbSet<Manufacturer> Manufacturers { get; set; }

    public virtual DbSet<Model> Models { get; set; }

    public virtual DbSet<MovementReason> MovementReasons { get; set; }

    public virtual DbSet<NetworkInterface> NetworkInterfaces { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<RelationType> RelationTypes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    public virtual DbSet<Software> Softwares { get; set; }

    public virtual DbSet<SoftwareLicense> SoftwareLicenses { get; set; }

    public virtual DbSet<SoftwareLicenseAssignment> SoftwareLicenseAssignments { get; set; }

    public virtual DbSet<SoftwareType> SoftwareTypes { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<VAssetSummary> VAssetSummaries { get; set; }

    public virtual DbSet<VLocationEquipmentSummary> VLocationEquipmentSummaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Acquisition>(entity =>
        {
            entity.HasKey(e => e.AcquisitionId).HasName("acquisitions_pkey");

            entity.ToTable("acquisitions", "inventario");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.AcquisitionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("acquisition_id");
            entity.Property(e => e.AcquiredAt).HasColumnName("acquired_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .HasDefaultValueSql("'MXN'::bpchar")
                .IsFixedLength()
                .HasColumnName("currency_code");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100)
                .HasColumnName("invoice_number");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.PurchaseOrder)
                .HasMaxLength(100)
                .HasColumnName("purchase_order");
            entity.Property(e => e.Subtotal)
                .HasPrecision(14, 2)
                .HasColumnName("subtotal");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.TaxAmount)
                .HasPrecision(14, 2)
                .HasColumnName("tax_amount");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(14, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken()
                .HasColumnName("version");
            entity.Property(e => e.WarrantyEnd).HasColumnName("warranty_end");
            entity.Property(e => e.WarrantyStart).HasColumnName("warranty_start");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AcquisitionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("acquisitions_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AcquisitionDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("acquisitions_deleted_by_fkey");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Acquisitions)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("acquisitions_supplier_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AcquisitionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("acquisitions_updated_by_fkey");
        });

        modelBuilder.Entity<AcquisitionAsset>(entity =>
        {
            entity.HasKey(e => new { e.AcquisitionId, e.AssetId }).HasName("acquisition_assets_pkey");

            entity.ToTable("acquisition_assets", "inventario");

            entity.Property(e => e.AcquisitionId).HasColumnName("acquisition_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.UnitCost)
                .HasPrecision(14, 2)
                .HasColumnName("unit_cost");

            entity.HasOne(d => d.Acquisition).WithMany(p => p.AcquisitionAssets)
                .HasForeignKey(d => d.AcquisitionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("acquisition_assets_acquisition_id_fkey");

            entity.HasOne(d => d.Asset).WithMany(p => p.AcquisitionAssets)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("acquisition_assets_asset_id_fkey");
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("app_users_pkey");

            entity.ToTable("app_users", "inventario");

            entity.HasIndex(e => e.Email, "app_users_email_key").IsUnique();

            entity.HasIndex(e => e.Username, "app_users_username_key").IsUnique();

            entity.Property(e => e.UserId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Email)
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'activo'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Username)
                .HasMaxLength(60)
                .HasColumnName("username");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InverseCreatedByNavigation)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("app_users_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.InverseDeletedByNavigation)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("app_users_deleted_by_fkey");

            entity.HasOne(d => d.Role).WithMany(p => p.AppUsers)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("app_users_role_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InverseUpdatedByNavigation)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("app_users_updated_by_fkey");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.AssetId).HasName("assets_pkey");

            entity.ToTable("assets", "inventario", tb => tb.HasComment("Identidad administrativa de cada equipo físico inventariable. No se elimina físicamente."));

            entity.HasIndex(e => e.InventoryCode, "assets_inventory_code_key").IsUnique();

            entity.HasIndex(e => e.CurrentLocationId, "ix_assets_active_location").HasFilter("(is_deleted = false)");

            entity.HasIndex(e => e.AssetTypeId, "ix_assets_active_type").HasFilter("(is_deleted = false)");

            entity.HasIndex(e => e.SerialNumber, "ux_assets_serial_nonempty")
                .IsUnique()
                .HasFilter("((serial_number IS NOT NULL) AND (btrim((serial_number)::text) <> ''::text))");

            entity.Property(e => e.AssetId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_id");
            entity.Property(e => e.AssetStatusId).HasColumnName("asset_status_id");
            entity.Property(e => e.AssetTypeId).HasColumnName("asset_type_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CurrentLocationId)
                .HasComment("Ubicación actual para consulta rápida; los cambios históricos están en asset_movements.")
                .HasColumnName("current_location_id");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Hostname)
                .HasMaxLength(150)
                .HasColumnName("hostname");
            entity.Property(e => e.InventoryCode)
                .HasMaxLength(40)
                .HasComment("Código de inventario estable e independiente de la ubicación (ej. TEC-PC-0001).")
                .HasColumnName("inventory_code");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.ManufacturerId).HasColumnName("manufacturer_id");
            entity.Property(e => e.ModelId).HasColumnName("model_id");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.SerialNumber)
                .HasMaxLength(150)
                .HasColumnName("serial_number");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.AssetStatus).WithMany(p => p.Assets)
                .HasForeignKey(d => d.AssetStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("assets_asset_status_id_fkey");

            entity.HasOne(d => d.AssetType).WithMany(p => p.Assets)
                .HasForeignKey(d => d.AssetTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("assets_asset_type_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("assets_created_by_fkey");

            entity.HasOne(d => d.CurrentLocation).WithMany(p => p.Assets)
                .HasForeignKey(d => d.CurrentLocationId)
                .HasConstraintName("assets_current_location_id_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("assets_deleted_by_fkey");

            entity.HasOne(d => d.Manufacturer).WithMany(p => p.Assets)
                .HasForeignKey(d => d.ManufacturerId)
                .HasConstraintName("assets_manufacturer_id_fkey");

            entity.HasOne(d => d.Model).WithMany(p => p.Assets)
                .HasForeignKey(d => d.ModelId)
                .HasConstraintName("assets_model_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("assets_updated_by_fkey");
        });

        modelBuilder.Entity<AssetAssignment>(entity =>
        {
            entity.HasKey(e => e.AssetAssignmentId).HasName("asset_assignments_pkey");

            entity.ToTable("asset_assignments", "inventario");

            entity.HasIndex(e => e.AssetId, "ix_assignments_asset");

            entity.Property(e => e.AssetAssignmentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_assignment_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.AssignmentTypeId).HasColumnName("assignment_type_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.ReturnedAt).HasColumnName("returned_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetAssignments)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_assignments_asset_id_fkey");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.AssetAssignmentAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_assignments_assigned_to_fkey");

            entity.HasOne(d => d.AssignmentType).WithMany(p => p.AssetAssignments)
                .HasForeignKey(d => d.AssignmentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_assignments_assignment_type_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetAssignmentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_assignments_created_by_fkey");

            entity.HasOne(d => d.Location).WithMany(p => p.AssetAssignments)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("asset_assignments_location_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetAssignmentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_assignments_updated_by_fkey");
        });

        modelBuilder.Entity<AssetComponent>(entity =>
        {
            entity.HasKey(e => e.AssetComponentId).HasName("asset_components_pkey");

            entity.ToTable("asset_components", "inventario", tb => tb.HasComment("Componentes internos de un activo; conserva instalación y retiro para historial."));

            entity.HasIndex(e => e.AssetId, "ix_components_asset");

            entity.Property(e => e.AssetComponentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_component_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.ComponentTypeId).HasColumnName("component_type_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description)
                .HasMaxLength(250)
                .HasColumnName("description");
            entity.Property(e => e.InstalledAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("installed_at");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.RemovedAt).HasColumnName("removed_at");
            entity.Property(e => e.SerialNumber)
                .HasMaxLength(150)
                .HasColumnName("serial_number");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetComponents)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_components_asset_id_fkey");

            entity.HasOne(d => d.ComponentType).WithMany(p => p.AssetComponents)
                .HasForeignKey(d => d.ComponentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_components_component_type_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetComponentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_components_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetComponentDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_components_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetComponentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_components_updated_by_fkey");
        });

        modelBuilder.Entity<AssetDisposal>(entity =>
        {
            entity.HasKey(e => e.AssetDisposalId).HasName("asset_disposals_pkey");

            entity.ToTable("asset_disposals", "inventario", tb => tb.HasComment("Baja administrativa; el asset original permanece en assets."));

            entity.HasIndex(e => e.AssetId, "asset_disposals_asset_id_key").IsUnique();

            entity.HasIndex(e => e.Folio, "asset_disposals_folio_key").IsUnique();

            entity.Property(e => e.AssetDisposalId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_disposal_id");
            entity.Property(e => e.ActDocumentPath).HasColumnName("act_document_path");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Dictamen).HasColumnName("dictamen");
            entity.Property(e => e.DisposalReasonId).HasColumnName("disposal_reason_id");
            entity.Property(e => e.DisposedAt)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("disposed_at");
            entity.Property(e => e.Folio)
                .HasMaxLength(100)
                .HasColumnName("folio");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithOne(p => p.AssetDisposal)
                .HasForeignKey<AssetDisposal>(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_disposals_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetDisposalCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_disposals_created_by_fkey");

            entity.HasOne(d => d.DisposalReason).WithMany(p => p.AssetDisposals)
                .HasForeignKey(d => d.DisposalReasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_disposals_disposal_reason_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetDisposalUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_disposals_updated_by_fkey");
        });

        modelBuilder.Entity<AssetMovement>(entity =>
        {
            entity.HasKey(e => e.AssetMovementId).HasName("asset_movements_pkey");

            entity.ToTable("asset_movements", "inventario", tb => tb.HasComment("Historial de movimientos. No debe borrarse para corregir ubicación."));

            entity.HasIndex(e => e.AssetId, "ix_movements_asset");

            entity.Property(e => e.AssetMovementId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_movement_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FromLocationId).HasColumnName("from_location_id");
            entity.Property(e => e.MovedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("moved_at");
            entity.Property(e => e.MovedBy).HasColumnName("moved_by");
            entity.Property(e => e.MovementReasonId).HasColumnName("movement_reason_id");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.ToLocationId).HasColumnName("to_location_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetMovements)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_movements_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetMovementCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_movements_created_by_fkey");

            entity.HasOne(d => d.FromLocation).WithMany(p => p.AssetMovementFromLocations)
                .HasForeignKey(d => d.FromLocationId)
                .HasConstraintName("asset_movements_from_location_id_fkey");

            entity.HasOne(d => d.MovedByNavigation).WithMany(p => p.AssetMovementMovedByNavigations)
                .HasForeignKey(d => d.MovedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_movements_moved_by_fkey");

            entity.HasOne(d => d.MovementReason).WithMany(p => p.AssetMovements)
                .HasForeignKey(d => d.MovementReasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_movements_movement_reason_id_fkey");

            entity.HasOne(d => d.ToLocation).WithMany(p => p.AssetMovementToLocations)
                .HasForeignKey(d => d.ToLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_movements_to_location_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetMovementUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_movements_updated_by_fkey");
        });

        modelBuilder.Entity<AssetOsHistory>(entity =>
        {
            entity.HasKey(e => e.AssetOsHistoryId).HasName("asset_os_history_pkey");

            entity.ToTable("asset_os_history", "inventario", tb => tb.HasComment("Historial de sistemas operativos instalados por activo."));

            entity.HasIndex(e => e.AssetId, "ux_current_asset_os_v2")
                .IsUnique()
                .HasFilter("(is_current AND (NOT is_deleted))");

            entity.Property(e => e.AssetOsHistoryId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_os_history_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.InstalledAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("installed_at");
            entity.Property(e => e.IsCurrent)
                .HasDefaultValue(true)
                .HasColumnName("is_current");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.OsName)
                .HasMaxLength(120)
                .HasColumnName("os_name");
            entity.Property(e => e.OsVersion)
                .HasMaxLength(60)
                .HasColumnName("os_version");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithOne(p => p.AssetOsHistory)
                .HasForeignKey<AssetOsHistory>(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_os_history_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetOsHistoryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_os_history_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetOsHistoryDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_os_history_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetOsHistoryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_os_history_updated_by_fkey");
        });

        modelBuilder.Entity<AssetRelation>(entity =>
        {
            entity.HasKey(e => e.AssetRelationId).HasName("asset_relations_pkey");

            entity.ToTable("asset_relations", "inventario");

            entity.Property(e => e.AssetRelationId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_relation_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PrimaryAssetId).HasColumnName("primary_asset_id");
            entity.Property(e => e.RelatedAssetId).HasColumnName("related_asset_id");
            entity.Property(e => e.RelationTypeId).HasColumnName("relation_type_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetRelationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_relations_created_by_fkey");

            entity.HasOne(d => d.PrimaryAsset).WithMany(p => p.AssetRelationPrimaryAssets)
                .HasForeignKey(d => d.PrimaryAssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_relations_primary_asset_id_fkey");

            entity.HasOne(d => d.RelatedAsset).WithMany(p => p.AssetRelationRelatedAssets)
                .HasForeignKey(d => d.RelatedAssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_relations_related_asset_id_fkey");

            entity.HasOne(d => d.RelationType).WithMany(p => p.AssetRelations)
                .HasForeignKey(d => d.RelationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_relations_relation_type_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetRelationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_relations_updated_by_fkey");
        });

        modelBuilder.Entity<AssetService>(entity =>
        {
            entity.HasKey(e => e.AssetServiceId).HasName("asset_services_pkey");

            entity.ToTable("asset_services", "inventario");

            entity.HasIndex(e => new { e.AssetId, e.ServiceId }, "asset_services_asset_id_service_id_key").IsUnique();

            entity.Property(e => e.AssetServiceId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_service_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetServices)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_services_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetServiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_services_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetServiceDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_services_deleted_by_fkey");

            entity.HasOne(d => d.Service).WithMany(p => p.AssetServices)
                .HasForeignKey(d => d.ServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_services_service_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetServiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_services_updated_by_fkey");
        });

        modelBuilder.Entity<AssetSoftware>(entity =>
        {
            entity.HasKey(e => e.AssetSoftwareId).HasName("asset_software_pkey");

            entity.ToTable("asset_software", "inventario", tb => tb.HasComment("Historial de software instalado por activo."));

            entity.HasIndex(e => new { e.AssetId, e.SoftwareId }, "ux_current_asset_software_v2")
                .IsUnique()
                .HasFilter("(is_current AND (NOT is_deleted))");

            entity.Property(e => e.AssetSoftwareId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_software_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.InstalledAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("installed_at");
            entity.Property(e => e.IsCurrent)
                .HasDefaultValue(true)
                .HasColumnName("is_current");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
            entity.Property(e => e.VersionInstalled)
                .HasMaxLength(60)
                .HasColumnName("version_installed");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetSoftwares)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_software_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetSoftwareCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_software_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetSoftwareDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_software_deleted_by_fkey");

            entity.HasOne(d => d.Software).WithMany(p => p.AssetSoftwares)
                .HasForeignKey(d => d.SoftwareId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_software_software_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetSoftwareUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_software_updated_by_fkey");
        });

        modelBuilder.Entity<AssetSpecification>(entity =>
        {
            entity.HasKey(e => e.AssetSpecificationId).HasName("asset_specifications_pkey");

            entity.ToTable("asset_specifications", "inventario");

            entity.HasIndex(e => new { e.AssetId, e.SpecificationKey }, "asset_specifications_asset_id_specification_key_key").IsUnique();

            entity.Property(e => e.AssetSpecificationId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_specification_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.SpecificationKey)
                .HasMaxLength(100)
                .HasColumnName("specification_key");
            entity.Property(e => e.SpecificationValue).HasColumnName("specification_value");
            entity.Property(e => e.Unit)
                .HasMaxLength(30)
                .HasColumnName("unit");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetSpecifications)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_specifications_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetSpecificationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_specifications_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetSpecificationDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_specifications_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetSpecificationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_specifications_updated_by_fkey");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("documents_pkey");

            entity.ToTable("documents", "inventario");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => new { e.OwnerType, e.OwnerId }, "ix_documents_owner")
                .HasFilter("is_deleted = false");

            entity.Property(e => e.DocumentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("document_id");
            entity.Property(e => e.OwnerType)
                .HasMaxLength(30)
                .HasColumnName("owner_type");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.StorageKey).HasColumnName("storage_key");
            entity.Property(e => e.MimeType)
                .HasMaxLength(100)
                .HasColumnName("mime_type");
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken()
                .HasColumnName("version");
        });

        modelBuilder.Entity<AssetStatus>(entity =>
        {
            entity.HasKey(e => e.AssetStatusId).HasName("asset_statuses_pkey");

            entity.ToTable("asset_statuses", "inventario");

            entity.HasIndex(e => e.Code, "asset_statuses_code_key").IsUnique();

            entity.Property(e => e.AssetStatusId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_status_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetStatusCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_statuses_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetStatusDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_statuses_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetStatusUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_statuses_updated_by_fkey");
        });

        modelBuilder.Entity<AssetType>(entity =>
        {
            entity.HasKey(e => e.AssetTypeId).HasName("asset_types_pkey");

            entity.ToTable("asset_types", "inventario");

            entity.HasIndex(e => e.Code, "asset_types_code_key").IsUnique();

            entity.Property(e => e.AssetTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("asset_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_types_updated_by_fkey");
        });

        modelBuilder.Entity<AssetWarranty>(entity =>
        {
            entity.HasKey(e => e.WarrantyId).HasName("asset_warranties_pkey");

            entity.ToTable("asset_warranties", "inventario");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.WarrantyId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("warranty_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.Coverage).HasColumnName("coverage");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.EndsOn).HasColumnName("ends_on");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.StartsOn).HasColumnName("starts_on");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Terms).HasColumnName("terms");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken()
                .HasColumnName("version");
            entity.Property(e => e.WarrantyNumber)
                .HasMaxLength(100)
                .HasColumnName("warranty_number");

            entity.HasOne(d => d.Asset).WithMany(p => p.AssetWarranties)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("asset_warranties_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssetWarrantyCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("asset_warranties_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssetWarrantyDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("asset_warranties_deleted_by_fkey");

            entity.HasOne(d => d.Supplier).WithMany(p => p.AssetWarranties)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("asset_warranties_supplier_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssetWarrantyUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("asset_warranties_updated_by_fkey");
        });

        modelBuilder.Entity<AssignmentType>(entity =>
        {
            entity.HasKey(e => e.AssignmentTypeId).HasName("assignment_types_pkey");

            entity.ToTable("assignment_types", "inventario");

            entity.HasIndex(e => e.Code, "assignment_types_code_key").IsUnique();

            entity.Property(e => e.AssignmentTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("assignment_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AssignmentTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("assignment_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.AssignmentTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("assignment_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AssignmentTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("assignment_types_updated_by_fkey");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("audit_log_pkey");

            entity.ToTable("audit_log", "inventario", tb => tb.HasComment("Registro inmutable de auditoría. No debe modificarse ni eliminarse desde la aplicación."));

            entity.HasIndex(e => new { e.TableName, e.RecordId }, "ix_audit_log_table_record");

            entity.Property(e => e.AuditLogId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("audit_log_id");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.ClientIp).HasColumnName("client_ip");
            entity.Property(e => e.NewData)
                .HasColumnType("jsonb")
                .HasColumnName("new_data");
            entity.Property(e => e.OldData)
                .HasColumnType("jsonb")
                .HasColumnName("old_data");
            entity.Property(e => e.PerformedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("performed_at");
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
            entity.Property(e => e.RecordId).HasColumnName("record_id");
            entity.Property(e => e.TableName)
                .HasMaxLength(120)
                .HasColumnName("table_name");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.PerformedBy)
                .HasConstraintName("audit_log_performed_by_fkey");
        });

        modelBuilder.Entity<BuildingSetting>(entity =>
        {
            entity.HasKey(e => e.BuildingSettingId).HasName("building_settings_pkey");

            entity.ToTable("building_settings", "inventario");

            entity.HasIndex(e => new { e.LocationId, e.SettingKey }, "building_settings_location_id_setting_key_key").IsUnique();

            entity.Property(e => e.BuildingSettingId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("building_setting_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.SettingKey)
                .HasMaxLength(100)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue).HasColumnName("setting_value");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BuildingSettingCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("building_settings_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.BuildingSettingDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("building_settings_deleted_by_fkey");

            entity.HasOne(d => d.Location).WithMany(p => p.BuildingSettings)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("building_settings_location_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BuildingSettingUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("building_settings_updated_by_fkey");
        });

        modelBuilder.Entity<ComponentType>(entity =>
        {
            entity.HasKey(e => e.ComponentTypeId).HasName("component_types_pkey");

            entity.ToTable("component_types", "inventario");

            entity.HasIndex(e => e.Code, "component_types_code_key").IsUnique();

            entity.Property(e => e.ComponentTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("component_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ComponentTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("component_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ComponentTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("component_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ComponentTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("component_types_updated_by_fkey");
        });

        modelBuilder.Entity<DisposalReason>(entity =>
        {
            entity.HasKey(e => e.DisposalReasonId).HasName("disposal_reasons_pkey");

            entity.ToTable("disposal_reasons", "inventario");

            entity.HasIndex(e => e.Code, "disposal_reasons_code_key").IsUnique();

            entity.Property(e => e.DisposalReasonId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("disposal_reason_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DisposalReasonCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("disposal_reasons_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.DisposalReasonDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("disposal_reasons_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DisposalReasonUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("disposal_reasons_updated_by_fkey");
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.IncidentId).HasName("incidents_pkey");

            entity.ToTable("incidents", "inventario");

            entity.HasIndex(e => e.AssetId, "ix_incidents_active_asset").HasFilter("(is_deleted = false)");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.IncidentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("incident_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Diagnosis).HasColumnName("diagnosis");
            entity.Property(e => e.IncidentPriorityId).HasColumnName("incident_priority_id");
            entity.Property(e => e.IncidentStatusId).HasColumnName("incident_status_id");
            entity.Property(e => e.IncidentTypeId).HasColumnName("incident_type_id");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.ReportedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("reported_at");
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.Solution).HasColumnName("solution");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version")
                .IsConcurrencyToken();

            entity.HasOne(d => d.Asset).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("incidents_asset_id_fkey");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.IncidentAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("incidents_assigned_to_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.IncidentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("incidents_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.IncidentDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("incidents_deleted_by_fkey");

            entity.HasOne(d => d.IncidentPriority).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.IncidentPriorityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("incidents_incident_priority_id_fkey");

            entity.HasOne(d => d.IncidentStatus).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.IncidentStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("incidents_incident_status_id_fkey");

            entity.HasOne(d => d.IncidentType).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.IncidentTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("incidents_incident_type_id_fkey");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.IncidentReportedByNavigations)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("incidents_reported_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.IncidentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("incidents_updated_by_fkey");
        });

        modelBuilder.Entity<IncidentPriority>(entity =>
        {
            entity.HasKey(e => e.IncidentPriorityId).HasName("incident_priorities_pkey");

            entity.ToTable("incident_priorities", "inventario");

            entity.HasIndex(e => e.Code, "incident_priorities_code_key").IsUnique();

            entity.Property(e => e.IncidentPriorityId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("incident_priority_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.IncidentPriorityCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("incident_priorities_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.IncidentPriorityDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("incident_priorities_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.IncidentPriorityUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("incident_priorities_updated_by_fkey");
        });

        modelBuilder.Entity<IncidentStatus>(entity =>
        {
            entity.HasKey(e => e.IncidentStatusId).HasName("incident_statuses_pkey");

            entity.ToTable("incident_statuses", "inventario");

            entity.HasIndex(e => e.Code, "incident_statuses_code_key").IsUnique();

            entity.Property(e => e.IncidentStatusId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("incident_status_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.IncidentStatusCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("incident_statuses_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.IncidentStatusDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("incident_statuses_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.IncidentStatusUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("incident_statuses_updated_by_fkey");
        });

        modelBuilder.Entity<IncidentType>(entity =>
        {
            entity.HasKey(e => e.IncidentTypeId).HasName("incident_types_pkey");

            entity.ToTable("incident_types", "inventario");

            entity.HasIndex(e => e.Code, "incident_types_code_key").IsUnique();

            entity.Property(e => e.IncidentTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("incident_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.IncidentTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("incident_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.IncidentTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("incident_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.IncidentTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("incident_types_updated_by_fkey");
        });

        modelBuilder.Entity<InventoryRecord>(entity =>
        {
            entity.HasKey(e => e.InventoryRecordId).HasName("inventory_records_pkey");

            entity.ToTable("inventory_records", "inventario", tb => tb.HasComment("Resultado de una verificación física contra la información de la BD."));

            entity.HasIndex(e => new { e.InventorySessionId, e.AssetId }, "inventory_records_inventory_session_id_asset_id_key").IsUnique();

            entity.Property(e => e.InventoryRecordId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("inventory_record_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Found).HasColumnName("found");
            entity.Property(e => e.InventorySessionId).HasColumnName("inventory_session_id");
            entity.Property(e => e.LocationFoundId).HasColumnName("location_found_id");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.InventoryRecords)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_records_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryRecordCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("inventory_records_created_by_fkey");

            entity.HasOne(d => d.InventorySession).WithMany(p => p.InventoryRecords)
                .HasForeignKey(d => d.InventorySessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_records_inventory_session_id_fkey");

            entity.HasOne(d => d.LocationFound).WithMany(p => p.InventoryRecords)
                .HasForeignKey(d => d.LocationFoundId)
                .HasConstraintName("inventory_records_location_found_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InventoryRecordUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("inventory_records_updated_by_fkey");
        });

        modelBuilder.Entity<InventorySession>(entity =>
        {
            entity.HasKey(e => e.InventorySessionId).HasName("inventory_sessions_pkey");

            entity.ToTable("inventory_sessions", "inventario");

            entity.Property(e => e.InventorySessionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("inventory_session_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'abierta'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventorySessionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("inventory_sessions_created_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InventorySessionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("inventory_sessions_updated_by_fkey");
        });

        modelBuilder.Entity<LicenseType>(entity =>
        {
            entity.HasKey(e => e.LicenseTypeId).HasName("license_types_pkey");

            entity.ToTable("license_types", "inventario");

            entity.HasIndex(e => e.Code, "license_types_code_key").IsUnique();

            entity.Property(e => e.LicenseTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("license_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.LicenseTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("license_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.LicenseTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("license_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.LicenseTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("license_types_updated_by_fkey");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("locations_pkey");

            entity.ToTable("locations", "inventario");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.ParentLocationId, "ix_locations_active_parent").HasFilter("(is_deleted = false)");

            entity.HasIndex(e => e.Code, "locations_code_key").IsUnique();

            entity.Property(e => e.LocationId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("location_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LocationTypeId).HasColumnName("location_type_id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.ParentLocationId).HasColumnName("parent_location_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken()
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.LocationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("locations_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.LocationDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("locations_deleted_by_fkey");

            entity.HasOne(d => d.LocationType).WithMany(p => p.Locations)
                .HasForeignKey(d => d.LocationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("locations_location_type_id_fkey");

            entity.HasOne(d => d.ParentLocation).WithMany(p => p.InverseParentLocation)
                .HasForeignKey(d => d.ParentLocationId)
                .HasConstraintName("locations_parent_location_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.LocationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("locations_updated_by_fkey");
        });

        modelBuilder.Entity<LocationType>(entity =>
        {
            entity.HasKey(e => e.LocationTypeId).HasName("location_types_pkey");

            entity.ToTable("location_types", "inventario");

            entity.HasIndex(e => e.Code, "location_types_code_key").IsUnique();

            entity.Property(e => e.LocationTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("location_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.LocationTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("location_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.LocationTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("location_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.LocationTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("location_types_updated_by_fkey");
        });

        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.HasKey(e => e.MaintenanceRecordId).HasName("maintenance_records_pkey");

            entity.ToTable("maintenance_records", "inventario");

            entity.HasIndex(e => e.AssetId, "ix_maintenance_asset");

            entity.Property(e => e.MaintenanceRecordId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("maintenance_record_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.MaintenanceTypeId).HasColumnName("maintenance_type_id");
            entity.Property(e => e.NextMaintenanceAt).HasColumnName("next_maintenance_at");
            entity.Property(e => e.PartsUsed).HasColumnName("parts_used");
            entity.Property(e => e.PerformedAt).HasColumnName("performed_at");
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
            entity.Property(e => e.ScheduledAt).HasColumnName("scheduled_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.MaintenanceRecords)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("maintenance_records_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaintenanceRecordCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("maintenance_records_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.MaintenanceRecordDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("maintenance_records_deleted_by_fkey");

            entity.HasOne(d => d.MaintenanceType).WithMany(p => p.MaintenanceRecords)
                .HasForeignKey(d => d.MaintenanceTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("maintenance_records_maintenance_type_id_fkey");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.MaintenanceRecordPerformedByNavigations)
                .HasForeignKey(d => d.PerformedBy)
                .HasConstraintName("maintenance_records_performed_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaintenanceRecordUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("maintenance_records_updated_by_fkey");
        });

        modelBuilder.Entity<MaintenanceType>(entity =>
        {
            entity.HasKey(e => e.MaintenanceTypeId).HasName("maintenance_types_pkey");

            entity.ToTable("maintenance_types", "inventario");

            entity.HasIndex(e => e.Code, "maintenance_types_code_key").IsUnique();

            entity.Property(e => e.MaintenanceTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("maintenance_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MaintenanceTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("maintenance_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.MaintenanceTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("maintenance_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MaintenanceTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("maintenance_types_updated_by_fkey");
        });

        modelBuilder.Entity<Manufacturer>(entity =>
        {
            entity.HasKey(e => e.ManufacturerId).HasName("manufacturers_pkey");

            entity.ToTable("manufacturers", "inventario");

            entity.HasIndex(e => e.Name, "manufacturers_name_key").IsUnique();

            entity.Property(e => e.ManufacturerId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("manufacturer_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ManufacturerCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("manufacturers_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ManufacturerDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("manufacturers_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ManufacturerUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("manufacturers_updated_by_fkey");
        });

        modelBuilder.Entity<Model>(entity =>
        {
            entity.HasKey(e => e.ModelId).HasName("models_pkey");

            entity.ToTable("models", "inventario");

            entity.HasIndex(e => new { e.ManufacturerId, e.ModelName }, "models_manufacturer_id_model_name_key").IsUnique();

            entity.Property(e => e.ModelId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("model_id");
            entity.Property(e => e.AssetTypeId).HasColumnName("asset_type_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.ManufacturerId).HasColumnName("manufacturer_id");
            entity.Property(e => e.ModelName)
                .HasMaxLength(150)
                .HasColumnName("model_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.AssetType).WithMany(p => p.Models)
                .HasForeignKey(d => d.AssetTypeId)
                .HasConstraintName("models_asset_type_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ModelCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("models_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ModelDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("models_deleted_by_fkey");

            entity.HasOne(d => d.Manufacturer).WithMany(p => p.Models)
                .HasForeignKey(d => d.ManufacturerId)
                .HasConstraintName("models_manufacturer_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ModelUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("models_updated_by_fkey");
        });

        modelBuilder.Entity<MovementReason>(entity =>
        {
            entity.HasKey(e => e.MovementReasonId).HasName("movement_reasons_pkey");

            entity.ToTable("movement_reasons", "inventario");

            entity.HasIndex(e => e.Code, "movement_reasons_code_key").IsUnique();

            entity.Property(e => e.MovementReasonId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("movement_reason_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MovementReasonCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("movement_reasons_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.MovementReasonDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("movement_reasons_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MovementReasonUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("movement_reasons_updated_by_fkey");
        });

        modelBuilder.Entity<NetworkInterface>(entity =>
        {
            entity.HasKey(e => e.NetworkInterfaceId).HasName("network_interfaces_pkey");

            entity.ToTable("network_interfaces", "inventario");

            entity.HasIndex(e => e.AssetId, "ix_network_interfaces_asset");

            entity.Property(e => e.NetworkInterfaceId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("network_interface_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.InterfaceName)
                .HasMaxLength(60)
                .HasColumnName("interface_name");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.MacAddress).HasColumnName("mac_address");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.NetworkInterfaces)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("network_interfaces_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.NetworkInterfaceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("network_interfaces_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.NetworkInterfaceDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("network_interfaces_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.NetworkInterfaceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("network_interfaces_updated_by_fkey");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("permissions_pkey");

            entity.ToTable("permissions", "inventario");

            entity.HasIndex(e => e.Code, "permissions_code_key").IsUnique();

            entity.Property(e => e.PermissionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("permission_id");
            entity.Property(e => e.Code)
                .HasMaxLength(80)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");
        });

        modelBuilder.Entity<RelationType>(entity =>
        {
            entity.HasKey(e => e.RelationTypeId).HasName("relation_types_pkey");

            entity.ToTable("relation_types", "inventario");

            entity.HasIndex(e => e.Code, "relation_types_code_key").IsUnique();

            entity.Property(e => e.RelationTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("relation_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RelationTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("relation_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.RelationTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("relation_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RelationTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("relation_types_updated_by_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");

            entity.ToTable("roles", "inventario");

            entity.HasIndex(e => e.Code, "roles_code_key").IsUnique();

            entity.Property(e => e.RoleId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("role_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken()
                .HasColumnName("version");

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("role_permissions_pkey");
                        j.ToTable("role_permissions", "inventario");
                        j.IndexerProperty<long>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<long>("PermissionId").HasColumnName("permission_id");
                    });
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.ServiceId).HasName("services_pkey");

            entity.ToTable("services", "inventario");

            entity.HasIndex(e => e.Name, "services_name_key").IsUnique();

            entity.Property(e => e.ServiceId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("service_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ServiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("services_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ServiceDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("services_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ServiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("services_updated_by_fkey");
        });

        modelBuilder.Entity<Software>(entity =>
        {
            entity.HasKey(e => e.SoftwareId).HasName("software_pkey");

            entity.ToTable("software", "inventario");

            entity.HasIndex(e => new { e.Name, e.Vendor }, "software_name_vendor_key").IsUnique();

            entity.Property(e => e.SoftwareId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("software_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.SoftwareTypeId).HasColumnName("software_type_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Vendor)
                .HasMaxLength(150)
                .HasColumnName("vendor");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SoftwareCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("software_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.SoftwareDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("software_deleted_by_fkey");

            entity.HasOne(d => d.SoftwareType).WithMany(p => p.Softwares)
                .HasForeignKey(d => d.SoftwareTypeId)
                .HasConstraintName("software_software_type_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SoftwareUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("software_updated_by_fkey");
        });

        modelBuilder.Entity<SoftwareLicense>(entity =>
        {
            entity.HasKey(e => e.LicenseId).HasName("software_licenses_pkey");

            entity.ToTable("software_licenses", "inventario");

            entity.Property(e => e.LicenseId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("license_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.LicenseKey).HasColumnName("license_key");
            entity.Property(e => e.LicenseTypeId).HasColumnName("license_type_id");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.QuantityPurchased).HasColumnName("quantity_purchased");
            entity.Property(e => e.SoftwareId).HasColumnName("software_id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.ValidFrom).HasColumnName("valid_from");
            entity.Property(e => e.ValidUntil).HasColumnName("valid_until");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SoftwareLicenseCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("software_licenses_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.SoftwareLicenseDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("software_licenses_deleted_by_fkey");

            entity.HasOne(d => d.LicenseType).WithMany(p => p.SoftwareLicenses)
                .HasForeignKey(d => d.LicenseTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("software_licenses_license_type_id_fkey");

            entity.HasOne(d => d.Software).WithMany(p => p.SoftwareLicenses)
                .HasForeignKey(d => d.SoftwareId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("software_licenses_software_id_fkey");

            entity.HasOne(d => d.Supplier).WithMany(p => p.SoftwareLicenses)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("software_licenses_supplier_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SoftwareLicenseUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("software_licenses_updated_by_fkey");
        });

        modelBuilder.Entity<SoftwareLicenseAssignment>(entity =>
        {
            entity.HasKey(e => e.LicenseAssignmentId).HasName("software_license_assignments_pkey");

            entity.ToTable("software_license_assignments", "inventario");

            entity.Property(e => e.LicenseAssignmentId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("license_assignment_id");
            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.LicenseId).HasColumnName("license_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.Asset).WithMany(p => p.SoftwareLicenseAssignments)
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("software_license_assignments_asset_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SoftwareLicenseAssignmentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("software_license_assignments_created_by_fkey");

            entity.HasOne(d => d.License).WithMany(p => p.SoftwareLicenseAssignments)
                .HasForeignKey(d => d.LicenseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("software_license_assignments_license_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SoftwareLicenseAssignmentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("software_license_assignments_updated_by_fkey");
        });

        modelBuilder.Entity<SoftwareType>(entity =>
        {
            entity.HasKey(e => e.SoftwareTypeId).HasName("software_types_pkey");

            entity.ToTable("software_types", "inventario");

            entity.HasIndex(e => e.Code, "software_types_code_key").IsUnique();

            entity.Property(e => e.SoftwareTypeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("software_type_id");
            entity.Property(e => e.Code)
                .HasMaxLength(40)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(120)
                .HasColumnName("name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SoftwareTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("software_types_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.SoftwareTypeDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("software_types_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SoftwareTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("software_types_updated_by_fkey");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("suppliers_pkey");

            entity.ToTable("suppliers", "inventario");

            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasIndex(e => e.Name, "suppliers_name_key").IsUnique();

            entity.Property(e => e.SupplierId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("supplier_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.ContactName)
                .HasMaxLength(150)
                .HasColumnName("contact_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Email)
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(180)
                .HasColumnName("name");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            entity.Property(e => e.TaxId)
                .HasMaxLength(30)
                .HasColumnName("tax_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken()
                .HasColumnName("version");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SupplierCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("suppliers_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.SupplierDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("suppliers_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SupplierUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("suppliers_updated_by_fkey");
        });

        modelBuilder.Entity<VAssetSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_asset_summary", "inventario");

            entity.Property(e => e.AssetId).HasColumnName("asset_id");
            entity.Property(e => e.AssetType)
                .HasMaxLength(40)
                .HasColumnName("asset_type");
            entity.Property(e => e.AssetTypeName)
                .HasMaxLength(120)
                .HasColumnName("asset_type_name");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Hostname)
                .HasMaxLength(150)
                .HasColumnName("hostname");
            entity.Property(e => e.InventoryCode)
                .HasMaxLength(40)
                .HasColumnName("inventory_code");
            entity.Property(e => e.LocationCode)
                .HasMaxLength(40)
                .HasColumnName("location_code");
            entity.Property(e => e.LocationName)
                .HasMaxLength(150)
                .HasColumnName("location_name");
            entity.Property(e => e.Manufacturer)
                .HasMaxLength(150)
                .HasColumnName("manufacturer");
            entity.Property(e => e.ModelName)
                .HasMaxLength(150)
                .HasColumnName("model_name");
            entity.Property(e => e.SerialNumber)
                .HasMaxLength(150)
                .HasColumnName("serial_number");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasColumnName("status");
            entity.Property(e => e.StatusName)
                .HasMaxLength(120)
                .HasColumnName("status_name");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Version).HasColumnName("version");
        });

        modelBuilder.Entity<VLocationEquipmentSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_location_equipment_summary", "inventario");

            entity.Property(e => e.LocationCode)
                .HasMaxLength(40)
                .HasColumnName("location_code");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.LocationName)
                .HasMaxLength(150)
                .HasColumnName("location_name");
            entity.Property(e => e.TotalAssets).HasColumnName("total_assets");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

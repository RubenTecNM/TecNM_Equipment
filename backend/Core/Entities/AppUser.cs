using System;
using System.Collections.Generic;

namespace TecnmInventario.Api.Core.Entities;

public partial class AppUser
{
    public long UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public long RoleId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public int Version { get; set; }

    public virtual ICollection<Acquisition> AcquisitionCreatedByNavigations { get; set; } = new List<Acquisition>();

    public virtual ICollection<Acquisition> AcquisitionDeletedByNavigations { get; set; } = new List<Acquisition>();

    public virtual ICollection<Acquisition> AcquisitionUpdatedByNavigations { get; set; } = new List<Acquisition>();

    public virtual ICollection<AssetAssignment> AssetAssignmentAssignedToNavigations { get; set; } = new List<AssetAssignment>();

    public virtual ICollection<AssetAssignment> AssetAssignmentCreatedByNavigations { get; set; } = new List<AssetAssignment>();

    public virtual ICollection<AssetAssignment> AssetAssignmentUpdatedByNavigations { get; set; } = new List<AssetAssignment>();

    public virtual ICollection<AssetComponent> AssetComponentCreatedByNavigations { get; set; } = new List<AssetComponent>();

    public virtual ICollection<AssetComponent> AssetComponentDeletedByNavigations { get; set; } = new List<AssetComponent>();

    public virtual ICollection<AssetComponent> AssetComponentUpdatedByNavigations { get; set; } = new List<AssetComponent>();

    public virtual ICollection<Asset> AssetCreatedByNavigations { get; set; } = new List<Asset>();

    public virtual ICollection<Asset> AssetDeletedByNavigations { get; set; } = new List<Asset>();

    public virtual ICollection<AssetDisposal> AssetDisposalCreatedByNavigations { get; set; } = new List<AssetDisposal>();

    public virtual ICollection<AssetDisposal> AssetDisposalUpdatedByNavigations { get; set; } = new List<AssetDisposal>();

    public virtual ICollection<AssetMovement> AssetMovementCreatedByNavigations { get; set; } = new List<AssetMovement>();

    public virtual ICollection<AssetMovement> AssetMovementMovedByNavigations { get; set; } = new List<AssetMovement>();

    public virtual ICollection<AssetMovement> AssetMovementUpdatedByNavigations { get; set; } = new List<AssetMovement>();

    public virtual ICollection<AssetOsHistory> AssetOsHistoryCreatedByNavigations { get; set; } = new List<AssetOsHistory>();

    public virtual ICollection<AssetOsHistory> AssetOsHistoryDeletedByNavigations { get; set; } = new List<AssetOsHistory>();

    public virtual ICollection<AssetOsHistory> AssetOsHistoryUpdatedByNavigations { get; set; } = new List<AssetOsHistory>();

    public virtual ICollection<AssetRelation> AssetRelationCreatedByNavigations { get; set; } = new List<AssetRelation>();

    public virtual ICollection<AssetRelation> AssetRelationUpdatedByNavigations { get; set; } = new List<AssetRelation>();

    public virtual ICollection<AssetService> AssetServiceCreatedByNavigations { get; set; } = new List<AssetService>();

    public virtual ICollection<AssetService> AssetServiceDeletedByNavigations { get; set; } = new List<AssetService>();

    public virtual ICollection<AssetService> AssetServiceUpdatedByNavigations { get; set; } = new List<AssetService>();

    public virtual ICollection<AssetSoftware> AssetSoftwareCreatedByNavigations { get; set; } = new List<AssetSoftware>();

    public virtual ICollection<AssetSoftware> AssetSoftwareDeletedByNavigations { get; set; } = new List<AssetSoftware>();

    public virtual ICollection<AssetSoftware> AssetSoftwareUpdatedByNavigations { get; set; } = new List<AssetSoftware>();

    public virtual ICollection<AssetSpecification> AssetSpecificationCreatedByNavigations { get; set; } = new List<AssetSpecification>();

    public virtual ICollection<AssetSpecification> AssetSpecificationDeletedByNavigations { get; set; } = new List<AssetSpecification>();

    public virtual ICollection<AssetSpecification> AssetSpecificationUpdatedByNavigations { get; set; } = new List<AssetSpecification>();

    public virtual ICollection<AssetStatus> AssetStatusCreatedByNavigations { get; set; } = new List<AssetStatus>();

    public virtual ICollection<AssetStatus> AssetStatusDeletedByNavigations { get; set; } = new List<AssetStatus>();

    public virtual ICollection<AssetStatus> AssetStatusUpdatedByNavigations { get; set; } = new List<AssetStatus>();

    public virtual ICollection<AssetType> AssetTypeCreatedByNavigations { get; set; } = new List<AssetType>();

    public virtual ICollection<AssetType> AssetTypeDeletedByNavigations { get; set; } = new List<AssetType>();

    public virtual ICollection<AssetType> AssetTypeUpdatedByNavigations { get; set; } = new List<AssetType>();

    public virtual ICollection<Asset> AssetUpdatedByNavigations { get; set; } = new List<Asset>();

    public virtual ICollection<AssetWarranty> AssetWarrantyCreatedByNavigations { get; set; } = new List<AssetWarranty>();

    public virtual ICollection<AssetWarranty> AssetWarrantyDeletedByNavigations { get; set; } = new List<AssetWarranty>();

    public virtual ICollection<AssetWarranty> AssetWarrantyUpdatedByNavigations { get; set; } = new List<AssetWarranty>();

    public virtual ICollection<AssignmentType> AssignmentTypeCreatedByNavigations { get; set; } = new List<AssignmentType>();

    public virtual ICollection<AssignmentType> AssignmentTypeDeletedByNavigations { get; set; } = new List<AssignmentType>();

    public virtual ICollection<AssignmentType> AssignmentTypeUpdatedByNavigations { get; set; } = new List<AssignmentType>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BuildingSetting> BuildingSettingCreatedByNavigations { get; set; } = new List<BuildingSetting>();

    public virtual ICollection<BuildingSetting> BuildingSettingDeletedByNavigations { get; set; } = new List<BuildingSetting>();

    public virtual ICollection<BuildingSetting> BuildingSettingUpdatedByNavigations { get; set; } = new List<BuildingSetting>();

    public virtual ICollection<ComponentType> ComponentTypeCreatedByNavigations { get; set; } = new List<ComponentType>();

    public virtual ICollection<ComponentType> ComponentTypeDeletedByNavigations { get; set; } = new List<ComponentType>();

    public virtual ICollection<ComponentType> ComponentTypeUpdatedByNavigations { get; set; } = new List<ComponentType>();

    public virtual AppUser? CreatedByNavigation { get; set; }

    public virtual AppUser? DeletedByNavigation { get; set; }

    public virtual ICollection<DisposalReason> DisposalReasonCreatedByNavigations { get; set; } = new List<DisposalReason>();

    public virtual ICollection<DisposalReason> DisposalReasonDeletedByNavigations { get; set; } = new List<DisposalReason>();

    public virtual ICollection<DisposalReason> DisposalReasonUpdatedByNavigations { get; set; } = new List<DisposalReason>();

    public virtual ICollection<Incident> IncidentAssignedToNavigations { get; set; } = new List<Incident>();

    public virtual ICollection<Incident> IncidentCreatedByNavigations { get; set; } = new List<Incident>();

    public virtual ICollection<Incident> IncidentDeletedByNavigations { get; set; } = new List<Incident>();

    public virtual ICollection<IncidentPriority> IncidentPriorityCreatedByNavigations { get; set; } = new List<IncidentPriority>();

    public virtual ICollection<IncidentPriority> IncidentPriorityDeletedByNavigations { get; set; } = new List<IncidentPriority>();

    public virtual ICollection<IncidentPriority> IncidentPriorityUpdatedByNavigations { get; set; } = new List<IncidentPriority>();

    public virtual ICollection<Incident> IncidentReportedByNavigations { get; set; } = new List<Incident>();

    public virtual ICollection<IncidentStatus> IncidentStatusCreatedByNavigations { get; set; } = new List<IncidentStatus>();

    public virtual ICollection<IncidentStatus> IncidentStatusDeletedByNavigations { get; set; } = new List<IncidentStatus>();

    public virtual ICollection<IncidentStatus> IncidentStatusUpdatedByNavigations { get; set; } = new List<IncidentStatus>();

    public virtual ICollection<IncidentType> IncidentTypeCreatedByNavigations { get; set; } = new List<IncidentType>();

    public virtual ICollection<IncidentType> IncidentTypeDeletedByNavigations { get; set; } = new List<IncidentType>();

    public virtual ICollection<IncidentType> IncidentTypeUpdatedByNavigations { get; set; } = new List<IncidentType>();

    public virtual ICollection<Incident> IncidentUpdatedByNavigations { get; set; } = new List<Incident>();

    public virtual ICollection<InventoryRecord> InventoryRecordCreatedByNavigations { get; set; } = new List<InventoryRecord>();

    public virtual ICollection<InventoryRecord> InventoryRecordUpdatedByNavigations { get; set; } = new List<InventoryRecord>();

    public virtual ICollection<InventorySession> InventorySessionCreatedByNavigations { get; set; } = new List<InventorySession>();

    public virtual ICollection<InventorySession> InventorySessionUpdatedByNavigations { get; set; } = new List<InventorySession>();

    public virtual ICollection<AppUser> InverseCreatedByNavigation { get; set; } = new List<AppUser>();

    public virtual ICollection<AppUser> InverseDeletedByNavigation { get; set; } = new List<AppUser>();

    public virtual ICollection<AppUser> InverseUpdatedByNavigation { get; set; } = new List<AppUser>();

    public virtual ICollection<LicenseType> LicenseTypeCreatedByNavigations { get; set; } = new List<LicenseType>();

    public virtual ICollection<LicenseType> LicenseTypeDeletedByNavigations { get; set; } = new List<LicenseType>();

    public virtual ICollection<LicenseType> LicenseTypeUpdatedByNavigations { get; set; } = new List<LicenseType>();

    public virtual ICollection<Location> LocationCreatedByNavigations { get; set; } = new List<Location>();

    public virtual ICollection<Location> LocationDeletedByNavigations { get; set; } = new List<Location>();

    public virtual ICollection<LocationType> LocationTypeCreatedByNavigations { get; set; } = new List<LocationType>();

    public virtual ICollection<LocationType> LocationTypeDeletedByNavigations { get; set; } = new List<LocationType>();

    public virtual ICollection<LocationType> LocationTypeUpdatedByNavigations { get; set; } = new List<LocationType>();

    public virtual ICollection<Location> LocationUpdatedByNavigations { get; set; } = new List<Location>();

    public virtual ICollection<MaintenanceRecord> MaintenanceRecordCreatedByNavigations { get; set; } = new List<MaintenanceRecord>();

    public virtual ICollection<MaintenanceRecord> MaintenanceRecordDeletedByNavigations { get; set; } = new List<MaintenanceRecord>();

    public virtual ICollection<MaintenanceRecord> MaintenanceRecordPerformedByNavigations { get; set; } = new List<MaintenanceRecord>();

    public virtual ICollection<MaintenanceRecord> MaintenanceRecordUpdatedByNavigations { get; set; } = new List<MaintenanceRecord>();

    public virtual ICollection<MaintenanceType> MaintenanceTypeCreatedByNavigations { get; set; } = new List<MaintenanceType>();

    public virtual ICollection<MaintenanceType> MaintenanceTypeDeletedByNavigations { get; set; } = new List<MaintenanceType>();

    public virtual ICollection<MaintenanceType> MaintenanceTypeUpdatedByNavigations { get; set; } = new List<MaintenanceType>();

    public virtual ICollection<Manufacturer> ManufacturerCreatedByNavigations { get; set; } = new List<Manufacturer>();

    public virtual ICollection<Manufacturer> ManufacturerDeletedByNavigations { get; set; } = new List<Manufacturer>();

    public virtual ICollection<Manufacturer> ManufacturerUpdatedByNavigations { get; set; } = new List<Manufacturer>();

    public virtual ICollection<Model> ModelCreatedByNavigations { get; set; } = new List<Model>();

    public virtual ICollection<Model> ModelDeletedByNavigations { get; set; } = new List<Model>();

    public virtual ICollection<Model> ModelUpdatedByNavigations { get; set; } = new List<Model>();

    public virtual ICollection<MovementReason> MovementReasonCreatedByNavigations { get; set; } = new List<MovementReason>();

    public virtual ICollection<MovementReason> MovementReasonDeletedByNavigations { get; set; } = new List<MovementReason>();

    public virtual ICollection<MovementReason> MovementReasonUpdatedByNavigations { get; set; } = new List<MovementReason>();

    public virtual ICollection<NetworkInterface> NetworkInterfaceCreatedByNavigations { get; set; } = new List<NetworkInterface>();

    public virtual ICollection<NetworkInterface> NetworkInterfaceDeletedByNavigations { get; set; } = new List<NetworkInterface>();

    public virtual ICollection<NetworkInterface> NetworkInterfaceUpdatedByNavigations { get; set; } = new List<NetworkInterface>();

    public virtual ICollection<RelationType> RelationTypeCreatedByNavigations { get; set; } = new List<RelationType>();

    public virtual ICollection<RelationType> RelationTypeDeletedByNavigations { get; set; } = new List<RelationType>();

    public virtual ICollection<RelationType> RelationTypeUpdatedByNavigations { get; set; } = new List<RelationType>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<Service> ServiceCreatedByNavigations { get; set; } = new List<Service>();

    public virtual ICollection<Service> ServiceDeletedByNavigations { get; set; } = new List<Service>();

    public virtual ICollection<Service> ServiceUpdatedByNavigations { get; set; } = new List<Service>();

    public virtual ICollection<Software> SoftwareCreatedByNavigations { get; set; } = new List<Software>();

    public virtual ICollection<Software> SoftwareDeletedByNavigations { get; set; } = new List<Software>();

    public virtual ICollection<SoftwareLicenseAssignment> SoftwareLicenseAssignmentCreatedByNavigations { get; set; } = new List<SoftwareLicenseAssignment>();

    public virtual ICollection<SoftwareLicenseAssignment> SoftwareLicenseAssignmentUpdatedByNavigations { get; set; } = new List<SoftwareLicenseAssignment>();

    public virtual ICollection<SoftwareLicense> SoftwareLicenseCreatedByNavigations { get; set; } = new List<SoftwareLicense>();

    public virtual ICollection<SoftwareLicense> SoftwareLicenseDeletedByNavigations { get; set; } = new List<SoftwareLicense>();

    public virtual ICollection<SoftwareLicense> SoftwareLicenseUpdatedByNavigations { get; set; } = new List<SoftwareLicense>();

    public virtual ICollection<SoftwareType> SoftwareTypeCreatedByNavigations { get; set; } = new List<SoftwareType>();

    public virtual ICollection<SoftwareType> SoftwareTypeDeletedByNavigations { get; set; } = new List<SoftwareType>();

    public virtual ICollection<SoftwareType> SoftwareTypeUpdatedByNavigations { get; set; } = new List<SoftwareType>();

    public virtual ICollection<Software> SoftwareUpdatedByNavigations { get; set; } = new List<Software>();

    public virtual ICollection<Supplier> SupplierCreatedByNavigations { get; set; } = new List<Supplier>();

    public virtual ICollection<Supplier> SupplierDeletedByNavigations { get; set; } = new List<Supplier>();

    public virtual ICollection<Supplier> SupplierUpdatedByNavigations { get; set; } = new List<Supplier>();

    public virtual AppUser? UpdatedByNavigation { get; set; }
}

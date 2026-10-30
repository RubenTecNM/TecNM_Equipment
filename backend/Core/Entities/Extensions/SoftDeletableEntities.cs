using TecnmInventario.Api.Core.Contracts;

namespace TecnmInventario.Api.Core.Entities;

// Declaraciones partial que agregan ISoftDeletable a las entidades scaffolded,
// sin modificar los archivos generados por EF Core (Document.cs, Supplier.cs,
// Acquisition.cs, AssetWarranty.cs quedan intactos). Todas ya tienen las 4
// propiedades (IsDeleted, DeletedAt, DeletedBy, Version) por estructura;
// esto solo declara el contrato formal para poder usarlas genéricamente
// en ISoftDeleteAuditor.

public partial class Document : ISoftDeletable
{
}

public partial class Supplier : ISoftDeletable
{
}

public partial class Acquisition : ISoftDeletable
{
}

public partial class AssetWarranty : ISoftDeletable
{
}

public partial class Location : ISoftDeletable
{
}

public partial class Incident : ISoftDeletable
{
}

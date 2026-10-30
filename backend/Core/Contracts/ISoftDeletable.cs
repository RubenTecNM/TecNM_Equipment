namespace TecnmInventario.Api.Core.Contracts;

/// <summary>
/// Contrato común para entidades con baja lógica + auditoría de borrado +
/// concurrencia optimista. Implementada como partial class sobre las
/// entidades scaffolded (Document, Supplier, Acquisition, AssetWarranty)
/// para no modificar los archivos generados por EF Core.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    long? DeletedBy { get; set; }
    int Version { get; set; }
}

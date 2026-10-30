namespace TecnmInventario.Api.Features.Documents;

/// <summary>
/// Verifica si el usuario actual tiene permiso para operar sobre documentos
/// de un OwnerType dado, según DocumentAuthorizationMap. Compartido por
/// todos los Handlers de Documents (Upload/Get/Download/Delete) para no
/// duplicar la lógica de "basta un permiso de la lista".
/// </summary>
public interface IDocumentAccessAuthorizer
{
    Task EnsureAuthorizedAsync(string ownerType, CancellationToken cancellationToken);
}

using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Assets.Shared;

public sealed record AssetStatusChangeResult(
    string PreviousStatusCode,
    string NewStatusCode,
    int NewVersion);

/// <summary>
/// Cambia el estado de un activo validando la máquina de estados y dejando
/// rastro en audit_log. NO abre su propia transacción: la abre el Handler que
/// lo invoca, para que el cambio de negocio propio y este cambio sean atómicos.
/// Exige que exista una transacción activa (falla de inmediato si no).
/// Nota: SaveChangesAsync vuelca TODO el trabajo pendiente del contexto, no
/// solo el activo.
/// </summary>
public interface IAssetStatusChanger
{
    Task<AssetStatusChangeResult> ChangeAsync(
        Asset asset,
        string newStatusCode,
        string reason,
        CancellationToken cancellationToken);
}

namespace TecnmInventario.Api.Features.Assets.UpdateAssetStatus;

public sealed record UpdateAssetStatusResponse(long AssetId, string StatusCode, int Version);

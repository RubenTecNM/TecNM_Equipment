using Mediator;

namespace TecnmInventario.Api.Features.Assets.UpdateAssetStatus;

public sealed record UpdateAssetStatusCommand(
    long AssetId,
    int ExpectedVersion,
    string NewStatusCode,
    string Reason) : ICommand<UpdateAssetStatusResponse>;

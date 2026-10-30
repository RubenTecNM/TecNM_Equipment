using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Warranties.CreateAssetWarranty;

public sealed record CreateAssetWarrantyCommand(
    long AssetId,
    long? SupplierId,
    string? WarrantyNumber,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string? Coverage,
    string? Terms) : ICommand<CreateAssetWarrantyResponse>, Common.Behaviors.ITransactionalCommand;

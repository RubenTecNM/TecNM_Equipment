using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Warranties.DeleteAssetWarranty;

public sealed record DeleteAssetWarrantyCommand(long WarrantyId, int ExpectedVersion)
    : ICommand<DeleteAssetWarrantyResponse>;

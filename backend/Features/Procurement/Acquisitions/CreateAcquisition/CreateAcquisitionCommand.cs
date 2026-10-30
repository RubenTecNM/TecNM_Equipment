using Mediator;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.CreateAcquisition;

public sealed record AcquisitionAssetInput(long AssetId, decimal? UnitCost);

public sealed record CreateAcquisitionCommand(
    long? SupplierId,
    string? PurchaseOrder,
    string? InvoiceNumber,
    DateOnly? AcquiredAt,
    decimal? Subtotal,
    decimal? TaxAmount,
    decimal? TotalAmount,
    string CurrencyCode,
    DateOnly? WarrantyStart,
    DateOnly? WarrantyEnd,
    string? Notes,
    IReadOnlyList<AcquisitionAssetInput> Assets) : ICommand<CreateAcquisitionResponse>, Common.Behaviors.ITransactionalCommand;

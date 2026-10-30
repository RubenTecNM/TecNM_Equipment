using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.CreateAcquisition;

public sealed class CreateAcquisitionHandler : ICommandHandler<CreateAcquisitionCommand, CreateAcquisitionResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateAcquisitionHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateAcquisitionResponse> Handle(
        CreateAcquisitionCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var acquisition = new Acquisition
        {
            SupplierId = command.SupplierId,
            PurchaseOrder = NullIfBlank(command.PurchaseOrder),
            InvoiceNumber = NullIfBlank(command.InvoiceNumber),
            AcquiredAt = command.AcquiredAt,
            Subtotal = command.Subtotal,
            TaxAmount = command.TaxAmount,
            TotalAmount = command.TotalAmount,
            CurrencyCode = command.CurrencyCode.ToUpperInvariant(),
            WarrantyStart = command.WarrantyStart,
            WarrantyEnd = command.WarrantyEnd,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        foreach (var assetInput in command.Assets)
        {
            acquisition.AcquisitionAssets.Add(new AcquisitionAsset
            {
                AssetId = assetInput.AssetId,
                UnitCost = assetInput.UnitCost
            });
        }

        // Un solo SaveChangesAsync inserta el padre (Acquisition) y los hijos
        // (AcquisitionAssets). La transacción explícita la abre
        // TransactionBehavior, que también confirma la fila de audit_log.
        _context.Acquisitions.Add(acquisition);
        await _context.SaveChangesAsync(cancellationToken);

        // El INSERT se audita después del SaveChanges para tener el id. El
        // snapshot incluye las líneas de acquisition_assets (activo + costo)
        // porque son parte del hecho registrado, no una tabla aparte.
        await _auditLog.LogAsync(
            tableName: "acquisitions",
            recordId: acquisition.AcquisitionId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                acquisition.SupplierId,
                acquisition.PurchaseOrder,
                acquisition.InvoiceNumber,
                acquisition.AcquiredAt,
                acquisition.Subtotal,
                acquisition.TaxAmount,
                acquisition.TotalAmount,
                acquisition.CurrencyCode,
                acquisition.WarrantyStart,
                acquisition.WarrantyEnd,
                acquisition.Notes,
                acquisition.Version,
                Assets = acquisition.AcquisitionAssets
                    .Select(a => new { a.AssetId, a.UnitCost })
                    .ToList()
            },
            cancellationToken);

        return new CreateAcquisitionResponse(
            acquisition.AcquisitionId, acquisition.AcquisitionAssets.Count, acquisition.Version);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

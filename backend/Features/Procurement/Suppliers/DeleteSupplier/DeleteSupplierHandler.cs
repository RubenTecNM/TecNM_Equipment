using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.DeleteSupplier;

public sealed class DeleteSupplierHandler : ICommandHandler<DeleteSupplierCommand, DeleteSupplierResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ISoftDeleteAuditor _softDeleteAuditor;

    public DeleteSupplierHandler(TecnmInventarioDbContext context, ISoftDeleteAuditor softDeleteAuditor)
    {
        _context = context;
        _softDeleteAuditor = softDeleteAuditor;
    }

    public async ValueTask<DeleteSupplierResponse> Handle(
        DeleteSupplierCommand command, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.SupplierId == command.SupplierId, cancellationToken);

        if (supplier is null)
        {
            throw new KeyNotFoundException("El proveedor solicitado no existe.");
        }

        // E1/E2: "en uso" incluye referencias en cualquier estado (también
        // borradas lógicamente), por eso IgnoreQueryFilters en cada comprobación.
        var hasAcquisitions = await _context.Acquisitions
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(a => a.SupplierId == command.SupplierId, cancellationToken);
        if (hasAcquisitions)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar el proveedor: tiene adquisiciones registradas.");
        }

        var hasWarranties = await _context.AssetWarranties
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(w => w.SupplierId == command.SupplierId, cancellationToken);
        if (hasWarranties)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar el proveedor: tiene garantías registradas.");
        }

        var hasLicenses = await _context.SoftwareLicenses
            .IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(l => l.SupplierId == command.SupplierId, cancellationToken);
        if (hasLicenses)
        {
            throw new ReferenceInUseException(
                "No se puede eliminar el proveedor: tiene licencias de software registradas.");
        }

        var beforeSnapshot = new
        {
            supplier.SupplierId,
            supplier.Name,
            supplier.TaxId,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.CreatedAt,
            supplier.CreatedBy,
            supplier.Version
        };

        await _softDeleteAuditor.DeleteAsync(
            supplier,
            recordId: supplier.SupplierId,
            expectedVersion: command.ExpectedVersion,
            tableName: "suppliers",
            snapshotBeforeDelete: beforeSnapshot,
            cancellationToken);

        return new DeleteSupplierResponse(supplier.SupplierId, true);
    }
}

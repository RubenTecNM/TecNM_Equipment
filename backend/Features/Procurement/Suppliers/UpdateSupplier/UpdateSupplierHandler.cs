using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;

public sealed class UpdateSupplierHandler : ICommandHandler<UpdateSupplierCommand, UpdateSupplierResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public UpdateSupplierHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<UpdateSupplierResponse> Handle(
        UpdateSupplierCommand command, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.SupplierId == command.SupplierId, cancellationToken);

        if (supplier is null)
        {
            throw new KeyNotFoundException("El proveedor solicitado no existe.");
        }

        // Concurrencia optimista antes del atajo de "sin cambios": una versión
        // vieja siempre es 409, aunque los datos enviados sean idénticos.
        if (supplier.Version != command.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "El proveedor fue modificado por otro usuario mientras tanto.");
        }

        var name = command.Name.Trim();
        var taxId = NullIfBlank(command.TaxId);
        var contactName = NullIfBlank(command.ContactName);
        var email = NullIfBlank(command.Email);
        var phone = NullIfBlank(command.Phone);
        var address = NullIfBlank(command.Address);
        var notes = NullIfBlank(command.Notes);

        // Un PUT idéntico no genera UPDATE, ni sube Version, ni se audita.
        var current = (
            supplier.Name,
            supplier.TaxId,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.Notes);
        var requested = (name, taxId, contactName, email, phone, address, notes);
        if (current == requested)
        {
            return new UpdateSupplierResponse(supplier.SupplierId, supplier.Name, supplier.Version);
        }

        // Snapshot "antes" antes de mutar.
        var before = new
        {
            supplier.Name,
            supplier.TaxId,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.Notes,
            supplier.Version
        };

        // Concurrencia optimista: comparamos contra la versión que el cliente
        // vio, no contra la que se acaba de leer.
        _context.Entry(supplier).Property(s => s.Version).OriginalValue = command.ExpectedVersion;

        supplier.Name = name;
        supplier.TaxId = taxId;
        supplier.ContactName = contactName;
        supplier.Email = email;
        supplier.Phone = phone;
        supplier.Address = address;
        supplier.Notes = notes;
        supplier.UpdatedBy = _currentUser.UserId;

        // UpdatedAt y Version los actualiza el trigger touch_version.
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(supplier).ReloadAsync(cancellationToken);

        var after = new
        {
            supplier.Name,
            supplier.TaxId,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.Notes,
            supplier.Version
        };

        await _auditLog.LogAsync(
            tableName: "suppliers",
            recordId: supplier.SupplierId,
            action: "UPDATE",
            oldData: before,
            newData: after,
            cancellationToken);

        return new UpdateSupplierResponse(supplier.SupplierId, supplier.Name, supplier.Version);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using Mediator;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Core.Entities;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.CreateSupplier;

public sealed class CreateSupplierHandler : ICommandHandler<CreateSupplierCommand, CreateSupplierResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public CreateSupplierHandler(
        TecnmInventarioDbContext context,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async ValueTask<CreateSupplierResponse> Handle(
        CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var supplier = new Supplier
        {
            Name = command.Name.Trim(),
            TaxId = NullIfBlank(command.TaxId),
            ContactName = NullIfBlank(command.ContactName),
            Email = NullIfBlank(command.Email),
            Phone = NullIfBlank(command.Phone),
            Address = NullIfBlank(command.Address),
            Notes = NullIfBlank(command.Notes),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            tableName: "suppliers",
            recordId: supplier.SupplierId,
            action: "INSERT",
            oldData: null,
            newData: new
            {
                supplier.Name,
                supplier.TaxId,
                supplier.ContactName,
                supplier.Email,
                supplier.Phone,
                supplier.Address,
                supplier.Notes,
                supplier.Version
            },
            cancellationToken);

        return new CreateSupplierResponse(supplier.SupplierId, supplier.Name, supplier.Version);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

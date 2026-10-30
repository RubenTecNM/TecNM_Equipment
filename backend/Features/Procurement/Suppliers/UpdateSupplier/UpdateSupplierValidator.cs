using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Suppliers.UpdateSupplier;

public class UpdateSupplierValidator : AbstractValidator<UpdateSupplierCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public UpdateSupplierValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Debe indicar la versión del proveedor que está editando.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proveedor es obligatorio.")
            .MaximumLength(DbFieldLengths.Suppliers.Name);

        RuleFor(x => x.TaxId).MaximumLength(DbFieldLengths.Suppliers.TaxId);
        RuleFor(x => x.ContactName).MaximumLength(DbFieldLengths.Suppliers.ContactName);
        RuleFor(x => x.Phone).MaximumLength(DbFieldLengths.Suppliers.Phone);

        RuleFor(x => x.Email)
            .MaximumLength(DbFieldLengths.Suppliers.Email)
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        // Único, pero excluyendo al propio proveedor que se está editando.
        RuleFor(x => x)
            .MustAsync(BeUniqueNameForOtherSupplier)
            .WithMessage("Ya existe otro proveedor con ese nombre.")
            .WithName("Name");
    }

    private async Task<bool> BeUniqueNameForOtherSupplier(UpdateSupplierCommand command, CancellationToken ct)
    {
        var trimmed = command.Name.Trim();
        return !await _context.Suppliers
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Name == trimmed && s.SupplierId != command.SupplierId, ct);
    }
}

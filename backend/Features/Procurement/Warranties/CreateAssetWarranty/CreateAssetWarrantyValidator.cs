using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Warranties.CreateAssetWarranty;

public class CreateAssetWarrantyValidator : AbstractValidator<CreateAssetWarrantyCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateAssetWarrantyValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.AssetId)
            .MustAsync(AssetExists).WithMessage("El activo especificado no existe.");

        RuleFor(x => x.SupplierId)
            .MustAsync(SupplierExists).WithMessage("El proveedor especificado no existe.")
            .When(x => x.SupplierId.HasValue);

        RuleFor(x => x.WarrantyNumber)
            .MaximumLength(DbFieldLengths.Warranties.WarrantyNumber);

        RuleFor(x => x)
            .Must(x => x.EndsOn is null || x.StartsOn is null || x.EndsOn >= x.StartsOn)
            .WithMessage("La fecha de fin de garantía no puede ser anterior a la fecha de inicio.")
            .WithName("EndsOn");
    }

    private async Task<bool> AssetExists(long id, CancellationToken ct)
        => await _context.Assets.AnyAsync(a => a.AssetId == id && !a.IsDeleted, ct);

    private async Task<bool> SupplierExists(long? id, CancellationToken ct)
        => await _context.Suppliers.AnyAsync(s => s.SupplierId == id, ct);
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.CreateAcquisition;

public class CreateAcquisitionValidator : AbstractValidator<CreateAcquisitionCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateAcquisitionValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("La moneda es obligatoria.")
            .Length(DbFieldLengths.Acquisitions.CurrencyCode)
            .WithMessage("El código de moneda debe tener 3 letras (ej. MXN, USD).");

        RuleFor(x => x.PurchaseOrder)
            .MaximumLength(DbFieldLengths.Acquisitions.PurchaseOrder);

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(DbFieldLengths.Acquisitions.InvoiceNumber);

        RuleFor(x => x.Subtotal).GreaterThanOrEqualTo(0).When(x => x.Subtotal.HasValue);
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0).When(x => x.TaxAmount.HasValue);
        RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0).When(x => x.TotalAmount.HasValue);

        RuleFor(x => x)
            .Must(x => x.WarrantyEnd is null || x.WarrantyStart is null || x.WarrantyEnd >= x.WarrantyStart)
            .WithMessage("La fecha de fin de garantía no puede ser anterior a la de inicio.")
            .WithName("WarrantyEnd");

        RuleFor(x => x.SupplierId)
            .MustAsync(SupplierExists).WithMessage("El proveedor especificado no existe.")
            .When(x => x.SupplierId.HasValue);

        RuleFor(x => x.Assets)
            .NotEmpty().WithMessage("Una adquisición debe incluir al menos un activo.");

        RuleForEach(x => x.Assets)
            .ChildRules(asset =>
            {
                asset.RuleFor(a => a.UnitCost).GreaterThanOrEqualTo(0).When(a => a.UnitCost.HasValue);
            });

        RuleFor(x => x.Assets)
            .MustAsync(AllAssetsExist).WithMessage("Uno o más activos especificados no existen.")
            .When(x => x.Assets.Count > 0);

        RuleFor(x => x.Assets)
            .Must(HaveNoDuplicateAssetIds).WithMessage("No se puede repetir el mismo activo dentro de la misma adquisición.");
    }

    private async Task<bool> SupplierExists(long? id, CancellationToken ct)
        => await _context.Suppliers.AnyAsync(s => s.SupplierId == id, ct);

    private async Task<bool> AllAssetsExist(IReadOnlyList<AcquisitionAssetInput> assets, CancellationToken ct)
    {
        var ids = assets.Select(a => a.AssetId).Distinct().ToList();
        var foundCount = await _context.Assets.CountAsync(a => ids.Contains(a.AssetId) && !a.IsDeleted, ct);
        return foundCount == ids.Count;
    }

    private static bool HaveNoDuplicateAssetIds(IReadOnlyList<AcquisitionAssetInput> assets)
        => assets.Select(a => a.AssetId).Distinct().Count() == assets.Count;
}

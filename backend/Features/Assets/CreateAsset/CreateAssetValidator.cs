using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.CreateAsset;

public class CreateAssetValidator : AbstractValidator<CreateAssetCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateAssetValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.InventoryCode)
            .NotEmpty().WithMessage("El código de inventario es obligatorio.")
            .MaximumLength(DbFieldLengths.Assets.InventoryCode)
            .MustAsync(BeUniqueInventoryCode).WithMessage("Ese código de inventario ya está en uso.");

        RuleFor(x => x.AssetTypeId)
            .MustAsync(AssetTypeExists).WithMessage("El tipo de activo especificado no existe.");

        RuleFor(x => x.ManufacturerId)
            .MustAsync(ManufacturerExists).WithMessage("El fabricante especificado no existe.")
            .When(x => x.ManufacturerId.HasValue);

        RuleFor(x => x.ModelId)
            .MustAsync(ModelExists).WithMessage("El modelo especificado no existe.")
            .When(x => x.ModelId.HasValue);

        RuleFor(x => x.CurrentLocationId)
            .MustAsync(LocationExists).WithMessage("La ubicación especificada no existe.")
            .When(x => x.CurrentLocationId.HasValue);
    }

    private async Task<bool> BeUniqueInventoryCode(string code, CancellationToken ct)
        => !await _context.Assets.AnyAsync(a => a.InventoryCode == code, ct);

    private async Task<bool> AssetTypeExists(long id, CancellationToken ct)
        => await _context.AssetTypes.AnyAsync(t => t.AssetTypeId == id && !t.IsDeleted, ct);

    private async Task<bool> ManufacturerExists(long? id, CancellationToken ct)
        => await _context.Manufacturers.AnyAsync(m => m.ManufacturerId == id && !m.IsDeleted, ct);

    private async Task<bool> ModelExists(long? id, CancellationToken ct)
        => await _context.Models.AnyAsync(m => m.ModelId == id && !m.IsDeleted, ct);

    private async Task<bool> LocationExists(long? id, CancellationToken ct)
        => await _context.Locations.AnyAsync(l => l.LocationId == id && !l.IsDeleted, ct);
}

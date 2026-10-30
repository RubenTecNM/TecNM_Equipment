using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.UpdateAsset;

public class UpdateAssetValidator : AbstractValidator<UpdateAssetCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public UpdateAssetValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Debe indicar la versión del activo que está editando.");

        RuleFor(x => x.ManufacturerId)
            .MustAsync(ManufacturerExists).WithMessage("El fabricante especificado no existe.")
            .When(x => x.ManufacturerId.HasValue);

        RuleFor(x => x.ModelId)
            .MustAsync(ModelExists).WithMessage("El modelo especificado no existe.")
            .When(x => x.ModelId.HasValue);

        RuleFor(x => x.SerialNumber).MaximumLength(DbFieldLengths.Assets.SerialNumber);
        RuleFor(x => x.Hostname).MaximumLength(DbFieldLengths.Assets.Hostname);
    }

    private async Task<bool> ManufacturerExists(long? id, CancellationToken ct)
        => await _context.Manufacturers.AnyAsync(m => m.ManufacturerId == id && !m.IsDeleted, ct);

    private async Task<bool> ModelExists(long? id, CancellationToken ct)
        => await _context.Models.AnyAsync(m => m.ModelId == id && !m.IsDeleted, ct);
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;

public class CreateAssetMovementValidator : AbstractValidator<CreateAssetMovementCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateAssetMovementValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.AssetId)
            .MustAsync(AssetExists).WithMessage("El activo especificado no existe.");

        RuleFor(x => x.ToLocationId)
            .MustAsync(LocationExists).WithMessage("La ubicación de destino no existe.");

        RuleFor(x => x.MovementReasonId)
            .MustAsync(ReasonExists).WithMessage("El motivo de movimiento no existe.");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del activo que está viendo (expectedVersion).");
    }

    private async Task<bool> AssetExists(long id, CancellationToken ct)
        => await _context.Assets.AnyAsync(a => a.AssetId == id && !a.IsDeleted, ct);

    private async Task<bool> LocationExists(long id, CancellationToken ct)
        => await _context.Locations.AnyAsync(l => l.LocationId == id && !l.IsDeleted, ct);

    private async Task<bool> ReasonExists(long id, CancellationToken ct)
        => await _context.MovementReasons.AnyAsync(r => r.MovementReasonId == id && !r.IsDeleted, ct);
}

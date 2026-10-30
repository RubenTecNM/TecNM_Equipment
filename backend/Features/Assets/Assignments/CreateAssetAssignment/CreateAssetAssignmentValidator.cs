using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;

public class CreateAssetAssignmentValidator : AbstractValidator<CreateAssetAssignmentCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public CreateAssetAssignmentValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.AssetId)
            .MustAsync(AssetExists).WithMessage("El activo especificado no existe.");

        RuleFor(x => x.AssignedTo)
            .MustAsync(UserExists)
            .WithMessage("El usuario asignado no existe o está inactivo.");

        RuleFor(x => x.AssignmentTypeId)
            .MustAsync(AssignmentTypeExists)
            .WithMessage("El tipo de asignación no existe.");

        RuleFor(x => x.LocationId)
            .MustAsync(LocationExists).WithMessage("La ubicación especificada no existe.")
            .When(x => x.LocationId.HasValue);

        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del activo que está viendo (expectedVersion).");
    }

    private async Task<bool> AssetExists(long id, CancellationToken ct)
        => await _context.Assets.AnyAsync(a => a.AssetId == id && !a.IsDeleted, ct);

    private async Task<bool> UserExists(long id, CancellationToken ct)
        => await _context.AppUsers
            .AnyAsync(u => u.UserId == id && !u.IsDeleted && u.Status == "activo", ct);

    private async Task<bool> AssignmentTypeExists(long id, CancellationToken ct)
        => await _context.AssignmentTypes
            .AnyAsync(t => t.AssignmentTypeId == id && !t.IsDeleted, ct);

    private async Task<bool> LocationExists(long? id, CancellationToken ct)
        => await _context.Locations
            .AnyAsync(l => l.LocationId == id && !l.IsDeleted, ct);
}

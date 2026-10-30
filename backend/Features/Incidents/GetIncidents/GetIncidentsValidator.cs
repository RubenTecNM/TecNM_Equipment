using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Incidents.GetIncidents;

public class GetIncidentsValidator : AbstractValidator<GetIncidentsQuery>
{
    private static readonly string[] ValidStatusCodes =
        IncidentStatusCodes.Open
            .Concat(new[] { IncidentStatusCodes.Resuelto, IncidentStatusCodes.Cerrado })
            .ToArray();

    private readonly TecnmInventarioDbContext _context;

    public GetIncidentsValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.StatusCode)
            .Must(code => ValidStatusCodes.Contains(code!.Trim().ToUpperInvariant()))
            .WithMessage("El estado de incidente especificado no es válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.StatusCode));

        RuleFor(x => x.PriorityCode)
            .MustAsync(PriorityExists).WithMessage("La prioridad especificada no existe.")
            .When(x => !string.IsNullOrWhiteSpace(x.PriorityCode));

        RuleFor(x => x.Search)
            .MaximumLength(100)
            .When(x => x.Search is not null);

        RuleFor(x => x.OnlyOpen)
            .Must(_ => false)
            .When(x => x.OnlyOpen == true && !string.IsNullOrWhiteSpace(x.StatusCode))
            .WithMessage("Use 'status' o 'onlyOpen', pero no ambos a la vez.");
    }

    private async Task<bool> PriorityExists(string? code, CancellationToken ct)
    {
        var normalized = code!.Trim().ToUpperInvariant();
        return await _context.IncidentPriorities
            .AnyAsync(p => p.Code == normalized && !p.IsDeleted, ct);
    }
}

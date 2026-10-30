using Mediator;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Documents.GetDocumentsByOwner;

public sealed class GetDocumentsByOwnerHandler
    : IQueryHandler<GetDocumentsByOwnerQuery, IReadOnlyList<GetDocumentsByOwnerResponse>>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly IDocumentAccessAuthorizer _accessAuthorizer;

    public GetDocumentsByOwnerHandler(
        TecnmInventarioDbContext context,
        IDocumentAccessAuthorizer accessAuthorizer)
    {
        _context = context;
        _accessAuthorizer = accessAuthorizer;
    }

    public async ValueTask<IReadOnlyList<GetDocumentsByOwnerResponse>> Handle(
        GetDocumentsByOwnerQuery query, CancellationToken cancellationToken)
    {
        await _accessAuthorizer.EnsureAuthorizedAsync(query.OwnerType, cancellationToken);

        return await _context.Documents
            .Where(d => !d.IsDeleted
                && d.OwnerType == query.OwnerType
                && d.OwnerId == query.OwnerId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new GetDocumentsByOwnerResponse(
                d.DocumentId,
                d.FileName,
                d.MimeType,
                d.SizeBytes,
                d.Description,
                d.CreatedAt,
                d.CreatedBy,
                d.Version))
            .ToListAsync(cancellationToken);
    }
}

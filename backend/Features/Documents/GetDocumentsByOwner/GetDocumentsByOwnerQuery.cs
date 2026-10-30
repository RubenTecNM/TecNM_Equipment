using Mediator;

namespace TecnmInventario.Api.Features.Documents.GetDocumentsByOwner;

public sealed record GetDocumentsByOwnerQuery(string OwnerType, long OwnerId)
    : IQuery<IReadOnlyList<GetDocumentsByOwnerResponse>>;

using Mediator;

namespace TecnmInventario.Api.Features.Documents.DownloadDocument;

public sealed record DownloadDocumentQuery(long DocumentId) : IQuery<DownloadDocumentResult>;

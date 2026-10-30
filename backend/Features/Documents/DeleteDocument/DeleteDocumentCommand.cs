using Mediator;

namespace TecnmInventario.Api.Features.Documents.DeleteDocument;

/// <summary>
/// ExpectedVersion es la versión del documento que el cliente vio por última
/// vez (ej. la que le devolvió GetDocumentsByOwner). Se usa para el control
/// de concurrencia optimista: si alguien más lo modificó mientras tanto, el
/// Handler falla con 409 en vez de borrar a ciegas sobre un cambio ajeno.
/// </summary>
public sealed record DeleteDocumentCommand(long DocumentId, int ExpectedVersion)
    : ICommand<DeleteDocumentResponse>;

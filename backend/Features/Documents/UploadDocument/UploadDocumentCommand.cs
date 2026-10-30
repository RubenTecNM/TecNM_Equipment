using Mediator;
using Microsoft.AspNetCore.Http;

namespace TecnmInventario.Api.Features.Documents.UploadDocument;

public sealed record UploadDocumentCommand(
    string OwnerType,
    long OwnerId,
    string? Description,
    IFormFile File) : ICommand<UploadDocumentResponse>, Common.Behaviors.ITransactionalCommand;

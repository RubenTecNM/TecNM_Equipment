using FluentValidation;

namespace TecnmInventario.Api.Features.Documents.DeleteDocument;

public class DeleteDocumentValidator : AbstractValidator<DeleteDocumentCommand>
{
    public DeleteDocumentValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Debe indicar la versión del documento que está eliminando.");
    }
}

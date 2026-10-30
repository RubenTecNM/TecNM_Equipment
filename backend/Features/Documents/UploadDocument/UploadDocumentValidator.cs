using FluentValidation;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Documents.UploadDocument;

public class UploadDocumentValidator : AbstractValidator<UploadDocumentCommand>
{
    private const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB, igual al CHECK de la BD

    private static readonly string[] AllowedMimeTypes =
        { "image/jpeg", "image/png", "application/pdf" };

    public UploadDocumentValidator()
    {
        RuleFor(x => x.OwnerType)
            .Must(t => DocumentOwnerTypes.All.Contains(t))
            .WithMessage($"OwnerType debe ser uno de: {string.Join(", ", DocumentOwnerTypes.All)}.");

        RuleFor(x => x.File)
            .NotNull().WithMessage("Debe adjuntar un archivo.")
            .Must(f => f.Length > 0).WithMessage("El archivo está vacío.")
            .Must(f => f.Length <= MaxSizeBytes).WithMessage("El archivo excede el límite de 5 MB.")
            .Must(f => AllowedMimeTypes.Contains(f.ContentType))
                .WithMessage($"Tipo de archivo no permitido. Formatos aceptados: {string.Join(", ", AllowedMimeTypes)}.")
            .Must(f => f.FileName.Length <= DbFieldLengths.Documents.FileName)
                .WithMessage($"El nombre del archivo no puede exceder {DbFieldLengths.Documents.FileName} caracteres.")
            .When(x => x.File is not null);
    }
}

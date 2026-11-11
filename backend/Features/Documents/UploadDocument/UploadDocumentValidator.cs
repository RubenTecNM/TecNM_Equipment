using FluentValidation;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Storage;

namespace TecnmInventario.Api.Features.Documents.UploadDocument;

public class UploadDocumentValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentValidator(IOptions<FileStorageOptions> storageOptions)
    {
        var maxSizeBytes = storageOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.OwnerType)
            .Must(t => DocumentOwnerTypes.All.Contains(t))
            .WithMessage($"OwnerType debe ser uno de: {string.Join(", ", DocumentOwnerTypes.All)}.");

        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Debe adjuntar un archivo.")
            .Must(f => f.Length > 0).WithMessage("El archivo está vacío.")
            .Must(f => f.Length <= maxSizeBytes)
                .WithMessage($"El archivo excede el límite de {FormatMegabytes(maxSizeBytes)}.")
            // ST4: el tipo lo decide el contenido, no la extensión ni el Content-Type.
            .Must(f => DocumentFileTypes.Detect(f) is not null)
                .WithMessage($"Tipo de archivo no permitido. Formatos aceptados: {DocumentFileTypes.AcceptedFormatsDescription}.")
            .Must(f => f.FileName.Length <= DocumentFileNames.MaxLength)
                .WithMessage($"El nombre del archivo no puede exceder {DocumentFileNames.MaxLength} caracteres.");
    }

    private static string FormatMegabytes(long bytes)
        => $"{bytes / (1024d * 1024d):0.##} MB";
}

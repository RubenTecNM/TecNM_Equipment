using Microsoft.AspNetCore.Http;

namespace TecnmInventario.Api.Features.Documents;

/// <summary>Tipo de archivo aceptado, detectado por su contenido.</summary>
public sealed record DocumentFileType(string Extension, string MimeType);

/// <summary>
/// ST4: el tipo sale de los primeros bytes del archivo (magic bytes). La
/// extensión y el Content-Type que declara el cliente no deciden nada.
/// Los valores de MimeType son los del CHECK de documents.mime_type.
/// </summary>
public static class DocumentFileTypes
{
    public static readonly DocumentFileType Pdf = new(".pdf", "application/pdf");
    public static readonly DocumentFileType Jpeg = new(".jpg", "image/jpeg");
    public static readonly DocumentFileType Png = new(".png", "image/png");

    /// <summary>Bytes necesarios para distinguir todos los tipos (el de PNG, 8).</summary>
    public const int HeaderLength = 8;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static string AcceptedFormatsDescription => "PDF, JPEG y PNG";

    /// <returns>El tipo detectado, o null si no es PDF, JPEG ni PNG.</returns>
    public static DocumentFileType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(PdfSignature)) return Pdf;
        if (header.StartsWith(JpegSignature)) return Jpeg;
        if (header.StartsWith(PngSignature)) return Png;
        return null;
    }

    /// <summary>Lee los primeros bytes del archivo subido y detecta su tipo.</summary>
    public static DocumentFileType? Detect(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[HeaderLength];
        var read = 0;
        while (read < header.Length)
        {
            var n = stream.Read(header[read..]);
            if (n == 0) break;
            read += n;
        }

        return Detect(header[..read]);
    }
}

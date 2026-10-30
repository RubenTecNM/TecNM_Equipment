using System.Text;
using Microsoft.AspNetCore.Http;

namespace TecnmInventario.Api.Tests.Integration;

internal static class TestUploadFile
{
    public static IFormFile Create(string fileName = "evidencia.pdf")
        => new FileFormFile(
            Encoding.UTF8.GetBytes("%PDF-1.4 integración"), fileName, "application/pdf");

    private sealed class FileFormFile : IFormFile
    {
        private readonly byte[] _content;

        public FileFormFile(byte[] content, string fileName, string contentType)
        {
            _content = content;
            FileName = fileName;
            ContentType = contentType;
        }

        public string ContentType { get; }

        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";

        public IHeaderDictionary Headers { get; } = new HeaderDictionary();

        public long Length => _content.Length;

        public string Name => "file";

        public string FileName { get; }

        public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => target.WriteAsync(_content, 0, _content.Length, cancellationToken);

        public Stream OpenReadStream() => new MemoryStream(_content, writable: false);
    }
}

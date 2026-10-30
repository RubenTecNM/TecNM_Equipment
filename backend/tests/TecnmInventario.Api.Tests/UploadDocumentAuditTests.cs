using System.Text;
using System.Text.Json;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Api.Features.Documents.UploadDocument;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Subetapa B2: UploadDocument guarda un archivo físico fuera de la BD. El
/// hook OnRollback debe borrarlo si la transacción del comando no se confirma,
/// y un fallo del borrado debe quedar en log sin tapar la excepción original.
/// </summary>
public class UploadDocumentAuditTests
{
    private static (string Root, LocalDiskFileStorageService Storage) CreateStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tecnm-upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var options = Options.Create(new FileStorageOptions { LocalPath = root });
        return (root, new LocalDiskFileStorageService(options));
    }

    private static IFormFile CreateFile(string fileName = "evidencia.pdf")
        => new FakeFormFile(
            Encoding.UTF8.GetBytes("%PDF-1.4 prueba B2"), fileName, "application/pdf");

    private static UploadDocumentHandler CreateHandler(
        TecnmInventario.Api.Data.TecnmInventarioDbContext context,
        IFileStorageService storage,
        ITransactionHooks hooks,
        TecnmInventario.Api.Common.Audit.IAuditLogService audit)
        => new(context, storage, new FakeDocumentAccessAuthorizer(), new FakeCurrentUser(), hooks, audit);

    private static UploadDocumentCommand CreateCommand(IFormFile file)
        => new("ACQUISITION", 1, "evidencia B2", file);

    [Fact]
    public async Task UploadDocument_guarda_archivo_y_audita_INSERT_con_storage_key_relativa()
    {
        var (root, storage) = CreateStorage();
        try
        {
            var context = TestDb.Create();
            using var _ = context;
            var audit = new RecordingAuditLogService();
            var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
            var handler = CreateHandler(context, storage, hooks, audit);

            var file = CreateFile();
            var response = await handler.Handle(CreateCommand(file), CancellationToken.None);

            var entry = Assert.Single(audit.Entries);
            Assert.Equal("documents", entry.TableName);
            Assert.Equal(response.DocumentId, entry.RecordId);
            Assert.Equal("INSERT", entry.Action);
            Assert.Null(entry.OldData);

            var document = context.Documents.Single(d => d.DocumentId == response.DocumentId);
            Assert.False(Path.IsPathRooted(document.StorageKey));
            Assert.True(File.Exists(Path.Combine(root, document.StorageKey)));

            var json = JsonSerializer.Serialize(entry.NewData);
            Assert.Contains(document.StorageKey, json, StringComparison.Ordinal);
            Assert.Contains("ACQUISITION", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Bajo_el_behavior_si_falla_la_auditoria_el_hook_borra_el_archivo()
    {
        var (root, storage) = CreateStorage();
        try
        {
            var context = TestDb.Create();
            using var _ = context;
            var audit = new RecordingAuditLogService { FailOnLog = true };
            var hooks = new TransactionHooks(NullLogger<TransactionHooks>.Instance);
            var handler = CreateHandler(context, storage, hooks, audit);
            var factory = new FakeTransactionScopeFactory();
            var behavior = new TransactionBehavior<UploadDocumentCommand, UploadDocumentResponse>(
                context,
                factory,
                hooks,
                NullLogger<TransactionBehavior<UploadDocumentCommand, UploadDocumentResponse>>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
                CreateCommand(CreateFile()),
                (message, ct) => handler.Handle(message, ct),
                CancellationToken.None).AsTask());

            // TransactionBehavior abrió la transacción (es el dueño) y el hook
            // de rollback borró el archivo ya guardado: no queda huérfano.
            Assert.Equal(1, factory.BeginCount);
            Assert.NotNull(factory.Scope);
            Assert.Equal(1, factory.Scope!.RollbackCount);
            Assert.Equal(0, factory.Scope.CommitCount);
            Assert.Empty(Directory.GetFiles(root));
            Assert.Empty(audit.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Si_falla_el_borrado_del_hook_se_registra_y_no_tapa_la_excepcion_original()
    {
        var context = TestDb.Create();
        using var _ = context;
        var audit = new RecordingAuditLogService { FailOnLog = true };
        var logger = new RecordingLogger<TransactionHooks>();
        var hooks = new TransactionHooks(logger);
        var handler = CreateHandler(context, new ThrowingDeleteStorage(), hooks, audit);
        var factory = new FakeTransactionScopeFactory();
        var behavior = new TransactionBehavior<UploadDocumentCommand, UploadDocumentResponse>(
            context,
            factory,
            hooks,
            NullLogger<TransactionBehavior<UploadDocumentCommand, UploadDocumentResponse>>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            CreateCommand(CreateFile()),
            (message, ct) => handler.Handle(message, ct),
            CancellationToken.None).AsTask());

        // La excepción original (auditoría) es la que llega al cliente.
        Assert.Equal("audit_log falló (simulado).", exception.Message);

        var logged = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains("compensación", logged.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(logged.Exception);
        Assert.Equal("no se pudo borrar (simulado)", logged.Exception!.Message);
    }

    private sealed class FakeFormFile : IFormFile
    {
        private readonly byte[] _content;

        public FakeFormFile(byte[] content, string fileName, string contentType)
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

    private sealed class ThrowingDeleteStorage : IFileStorageService
    {
        public Task<string> SaveAsync(
            Stream content, string originalFileName, CancellationToken cancellationToken)
            => Task.FromResult("clave-simulada.pdf");

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
            => throw new InvalidOperationException("no se pudo borrar (simulado)");
    }
}

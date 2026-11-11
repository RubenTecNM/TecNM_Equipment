namespace TecnmInventario.Api.Common.Middleware;

/// <summary>
/// Lista blanca de mensajes de <c>RAISE EXCEPTION</c> (SQLSTATE P0001) de los
/// triggers que SÍ se pueden mostrar al usuario tal cual: en español, sin
/// nombres de tablas, columnas ni ids internos, y útiles para quien los lee.
/// La coincidencia es EXACTA (texto completo, distingue mayúsculas): un
/// mensaje con parámetros (p. ej. "...%...") no puede estar en la lista.
/// Cualquier otro P0001 sale como un mensaje genérico y su texto original va
/// solo al log (ver ExceptionHandlingMiddleware).
///
/// Estado actual (Lote 5.6): el único RAISE EXCEPTION de scripts/*.sql
/// (seed-documents.sql, validate_document_owner) es INTERNO, así que la lista
/// de producción está vacía. Para publicar un mensaje funcional nuevo, añade
/// aquí su texto exacto; los tests pueden inyectar otra lista por DI.
/// </summary>
public sealed class FunctionalRaiseMessages
{
    /// <summary>Lista de producción.</summary>
    public static readonly FunctionalRaiseMessages Default = new(Array.Empty<string>());

    private readonly HashSet<string> _messages;

    public FunctionalRaiseMessages(IEnumerable<string> messages)
    {
        _messages = new HashSet<string>(messages, StringComparer.Ordinal);
    }

    public bool Contains(string message) => _messages.Contains(message);
}

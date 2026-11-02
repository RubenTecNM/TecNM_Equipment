using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Common.Persistence;

/// <summary>
/// Datos de la fila del activo leídos DESPUÉS de tomar el lock. Cualquier
/// decisión que dependa del estado del activo o de otras filas (por ejemplo,
/// contar incidentes abiertos) debe recalcularse con estos valores.
/// </summary>
public readonly record struct LockedAssetRow(
    long AssetId, long AssetStatusId, string AssetStatusCode, int Version);

/// <summary>
/// Bloquea la fila del activo con <c>SELECT ... FOR UPDATE</c> dentro de la
/// transacción del llamador. Es el PRIMER lock de la transacción: serializa
/// las decisiones que dependen de otras filas (incidentes abiertos,
/// asignaciones vigentes) y evita que dos transacciones cuenten a la vez
/// estados que la otra todavía no ha confirmado.
/// </summary>
public static class AssetRowLock
{
    public static async Task<LockedAssetRow> LockAsync(
        TecnmInventarioDbContext context, long assetId, CancellationToken cancellationToken)
    {
        // Mismo guard que AssetStatusChanger: sin transacción del llamador, el
        // lock se liberaría al terminar la sentencia y no protegería nada.
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "AssetRowLock requiere una transacción activa iniciada por el llamador.");

        // NOTA: si algún día se habilita EnableRetryOnFailure en UseNpgsql,
        // estos handlers deberán ejecutar TODA su transacción dentro de
        // context.Database.CreateExecutionStrategy() (como TransactionBehavior).
        // Con reintentos habilitados y una transacción manual abierta, el
        // reintento de EF falla al intentar reutilizar la transacción; hoy
        // Program.cs y los tests usan UseNpgsql sin reintentos.
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            SELECT a.asset_id, a.asset_status_id, a.version,
                   (SELECT s.code
                    FROM inventario.asset_statuses s
                    WHERE s.asset_status_id = a.asset_status_id)
            FROM inventario.assets a
            WHERE a.asset_id = @asset_id
            FOR UPDATE
            """;

        // Parámetro real: nunca se concatena el id en el SQL.
        var parameter = command.CreateParameter();
        parameter.ParameterName = "asset_id";
        parameter.Value = assetId;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KeyNotFoundException("El activo solicitado no existe.");
        }

        return new LockedAssetRow(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(3),
            reader.GetInt32(2));
    }
}

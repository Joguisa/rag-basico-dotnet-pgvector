using Npgsql;

namespace RagBasico.Data;

public static class SchemaCheck
{
    // verifica en tiempo de ejecucion si la tabla chunks existe
    public static async Task<bool> E(NpgsqlDataSource dataSource, CancellationToken ct = default)
    {// await using significa que, al salir del bloque, la conexión se libera de vuelta al pool automáticamente (aunque haya una excepción).
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT to_regclass('public.chunks') IS NOT NULL", conn); // comprueba si existe la tabla chunks
        var result = await cmd.ExecuteScalarAsync(ct); // ExecuteScalarAsync: ejecuta la query y devuelve un solo valor(una sola celda), no un dataset completo
        return result is true; // chequear si el object devuelto es literalmente true
    }
}

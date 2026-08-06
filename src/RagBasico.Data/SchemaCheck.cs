using Npgsql;

namespace RagBasico.Data;

public static class SchemaCheck
{
    public static async Task<bool> ChunksTableExistsAsync(NpgsqlDataSource dataSource, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT to_regclass('public.chunks') IS NOT NULL", conn);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }
}

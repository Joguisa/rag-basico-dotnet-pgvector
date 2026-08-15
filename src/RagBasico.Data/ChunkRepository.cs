using Npgsql;
using NpgsqlTypes;
using Pgvector;
using RagBasico.Core.Persistence;

namespace RagBasico.Data;

public sealed class ChunkRepository : IChunkRepository
{
    private readonly NpgsqlDataSource _dataSource;

    // insertar chunks ya procesados en la tabla chunks, no sabe como se genero el embedding ni como se partio el texto
    public ChunkRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InsertManyAsync(IReadOnlyList<StoredChunk> chunks, CancellationToken ct = default)
    {
        // ON CONFLICT: si (source, chunk_index) ya existe por una reingesta del mismo documento
        // pisamos content/embedding en vez de duplicar la fila o fallar por violar el constraint único.
        var sql = """
            INSERT INTO chunks (source, chunk_index, content, embedding)
            VALUES (@source, @chunkIndex, @content, @embedding)
            ON CONFLICT (source, chunk_index)
            DO UPDATE SET content = EXCLUDED.content, embedding = EXCLUDED.embedding
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await conn.BeginTransactionAsync(ct);

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn, transaction);

            var source = cmd.Parameters.Add(new NpgsqlParameter("@source", NpgsqlDbType.Varchar));
            var chunkIndex = cmd.Parameters.Add(new NpgsqlParameter("@chunkIndex", NpgsqlDbType.Integer));
            var content = cmd.Parameters.Add(new NpgsqlParameter("@content", NpgsqlDbType.Varchar));
            var embedding = cmd.Parameters.Add(new NpgsqlParameter<Vector> { ParameterName = "@embedding" });

            await cmd.PrepareAsync();

            foreach (var chunk in chunks)
            {
                source.Value = chunk.Source;
                chunkIndex.Value = chunk.ChunkIndex;
                content.Value = chunk.Content;
                embedding.Value = new Vector(chunk.Embedding);

                await cmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteOrphanedChunksAsync(string source, int keepCount, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM chunks WHERE source = @source AND chunk_index >= @keepCount";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.Add(new NpgsqlParameter("@source", NpgsqlDbType.Varchar) { Value = source });
        cmd.Parameters.Add(new NpgsqlParameter("@keepCount", NpgsqlDbType.Integer) { Value = keepCount });

        await cmd.ExecuteNonQueryAsync(ct);
    }
}

using Npgsql;
using NpgsqlTypes;
using Pgvector;
using RagBasico.Core.Persistence;
using System.Data;

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

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken ct = default)
    {
        var vector = new Vector(queryEmbedding); // convertir float a vector

        var sql = """
            SELECT source, chunk_index, content, embedding <=>
            @queryEmbedding AS distance
            FROM chunks
            ORDER BY distance
            LIMIT @topK
            """;

        // await using // Cuando termine de usar este objeto, liberalo correctamente de forma asíncrona.
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.Add(new NpgsqlParameter<Vector>("queryEmbedding", vector));
        cmd.Parameters.AddWithValue("topK", topK);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<RetrievedChunk>();

        int sourceOrdinal = reader.GetOrdinal("source");
        int chunkOrdinal = reader.GetOrdinal("chunk_index");
        int contentOrdinal = reader.GetOrdinal("content");
        int distanceOrdinal = reader.GetOrdinal("distance");

        while (await reader.ReadAsync(ct))
        {
            results.Add(new RetrievedChunk(
                reader.GetString(sourceOrdinal),
                reader.GetInt32(chunkOrdinal),
                reader.GetString(contentOrdinal),
                reader.GetFloat(distanceOrdinal)
            ));
        }

        return results;

    }
}

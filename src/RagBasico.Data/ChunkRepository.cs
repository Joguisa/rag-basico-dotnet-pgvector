using Npgsql;

namespace RagBasico.Data;

public sealed class ChunkRepository
{
    // insertar chunks ya procesados en la tabla chunks, no sabe como se genero el embedding ni como se partio el texto
    public ChunkRepository(NpgsqlDataSource dataSource);

    public Task InsertManyAsync(IReadOnlyList<StoredChunk> chunks, CancellationToken ct = default);

}

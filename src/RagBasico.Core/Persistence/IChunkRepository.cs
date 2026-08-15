namespace RagBasico.Core.Persistence
{
    public interface IChunkRepository
    {
        Task InsertManyAsync(IReadOnlyList<StoredChunk> chunks, CancellationToken ct = default);

        // borra los chunks de 'source' cuyo chunk_index quedó fuera del documento reingestado
        // (evita chunks huérfanos cuando una reingesta produce menos chunks que la anterior)
        Task DeleteOrphanedChunksAsync(string source, int keepCount, CancellationToken ct = default);
    }
}

namespace RagBasico.Core.Persistence
{
    public interface IChunkRepository
    {
        Task InsertManyAsync(IReadOnlyList<StoredChunk> chunks, CancellationToken ct = default);
    }
}

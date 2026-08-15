using RagBasico.Core.Chunking;
using RagBasico.Core.Embeddings;
using RagBasico.Core.Persistence;

namespace RagBasico.Core.Ingestion;

public sealed class DocumentIngestionService
{
    private readonly Chunker _chunker;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IChunkRepository _chunkRepository;
    private readonly int _batchSize;
    private readonly int _chunkSize;
    private readonly int _overlap;

    // coordinar el flujo completo para UN documento
    // chunkear -> agrupar en lotes de batchSize -> pedir embeddings por lote -> emparejar chunk+vector -> guardar
    public DocumentIngestionService(
        Chunker chunker,
        IEmbeddingClient embeddingClient,
        IChunkRepository chunkRepository,
        int batchSize,
        int chunkSize, 
        int overlap)
    {
        _chunker = chunker;
        _embeddingClient = embeddingClient;
        _chunkRepository = chunkRepository;
        _batchSize = batchSize;
        _chunkSize = chunkSize;
        _overlap = overlap;
    }

    public async Task<int> IngestAsync(string source, string documentText, CancellationToken ct = default)
    {
        var chunks = _chunker.Split(documentText, _chunkSize, _overlap);

        var batchs = chunks.Chunk(_batchSize);

        foreach (var batch in batchs)
        {
            var contents = batch.Select(c => c.Content).ToList();
            var embeddings = await _embeddingClient.GetEmbeddingsAsync(contents, ct);

            var storedChunks = batch.Zip(embeddings, (chunk, embedding) =>
                new StoredChunk(source, chunk.Index,chunk.Content, embedding)).ToList();

            await _chunkRepository.InsertManyAsync(storedChunks, ct);
        }

        // limpia chunks de reingestas anteriores que quedaron fuera del documento actual
        await _chunkRepository.DeleteOrphanedChunksAsync(source, chunks.Count, ct);

        return chunks.Count;
    }
}


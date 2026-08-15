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
    // coordinar el flujo completo para UN documento
    // chunkear -> agrupar en lotes de batchSize -> pedir embeddings por lote -> emparejar chunk+vector -> guardar
    public DocumentIngestionService(
        Chunker chunker,
        IEmbeddingClient embeddingClient,
        IChunkRepository chunkRepository,
        int batchSize)
    {
        _chunker = chunker;
        _embeddingClient = embeddingClient;
        _chunkRepository = chunkRepository;
        _batchSize = batchSize;
    }

    public async Task IngestAsync(string source, string documentText, CancellationToken ct = default)
    {
        var chunks = _chunker.Split(documentText);

        var batchs = chunks.Chunk(_batchSize);
        
        foreach (var batch in batchs)
        {
            var contents = batch.Select(c => c.Content).ToList();
            var embeddings = await _embeddingClient.GetEmbeddingsAsync(contents, ct);

            var storedChunks = batch.Zip(embeddings, (chunk, embedding) =>
                new StoredChunk(source, chunk.Index,chunk.Content, embedding)).ToList();

            await _chunkRepository.InsertManyAsync(storedChunks, ct);
        }
    }
}


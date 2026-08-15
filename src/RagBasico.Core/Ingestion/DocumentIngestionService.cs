using RagBasico.Core.Chunking;
using RagBasico.Core.Embeddings;

namespace RagBasico.Core.Ingestion;

public sealed class DocumentIngestionService
{
    // coordinar el flujo completo para UN documento
    // chunkear -> agrupar en lotes de batchSize -> pedir embeddings por lote -> emparejar chunk+vector -> guardar
    public DocumentIngestionService(
        Chunker chunker,
        OllamaEmbeddingClient embeddingClient,
        ChunkRepository chunkRepository,
        int batchSize);

    public Task IngestAsync(string source, string documentTest, CancellationToken ct = default);
}


using RagBasico.Core.Embeddings;
using RagBasico.Core.Persistence;

namespace RagBasico.Core.Retrieval
{
    public sealed class RetrievalService
    {
        private readonly IEmbeddingClient _embeddingClient;
        private readonly IChunkRepository _chunkRepository;
        private readonly int _topK;

        public RetrievalService(
            IEmbeddingClient embeddingClient,
            IChunkRepository chunkRepository,
            int topK)
        {
            _embeddingClient = embeddingClient;
            _chunkRepository = chunkRepository;
            _topK = topK;
        }

        public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(string question, CancellationToken ct = default)
        {
            var qst = new List<string> { question };

            var embeddings = await _embeddingClient.GetEmbeddingsAsync(qst, ct);
            var vector = embeddings[0];
            var tops = await _chunkRepository.SearchAsync(vector, _topK, ct);

            return tops;
        }
    }
}

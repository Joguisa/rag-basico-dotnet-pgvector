namespace RagBasico.Core.Embeddings
{
    public interface IEmbeddingClient
    {
        Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    }
}

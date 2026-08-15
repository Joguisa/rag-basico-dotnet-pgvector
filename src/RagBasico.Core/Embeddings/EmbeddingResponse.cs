using System.Text.Json.Serialization;

namespace RagBasico.Core.Embeddings
{
    public sealed record EmbeddingResponse(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings
    );
}

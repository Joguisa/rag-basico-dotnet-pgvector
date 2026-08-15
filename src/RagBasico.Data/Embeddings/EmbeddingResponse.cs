using System.Text.Json.Serialization;

namespace RagBasico.Data.Embeddings
{
    public sealed record EmbeddingResponse(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings
    );
}

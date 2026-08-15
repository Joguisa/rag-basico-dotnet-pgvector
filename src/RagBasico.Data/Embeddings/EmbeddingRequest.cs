using System.Text.Json.Serialization;

namespace RagBasico.Data.Embeddings
{
    public sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IEnumerable<string> Input//string[] Input
    );
}

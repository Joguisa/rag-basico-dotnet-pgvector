using System.Text.Json.Serialization;

namespace RagBasico.Data.Generation
{
    public sealed record ChatMessageDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content
    );
}

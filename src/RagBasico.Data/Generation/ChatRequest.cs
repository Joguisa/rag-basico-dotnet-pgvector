using System.Text.Json.Serialization;

namespace RagBasico.Data.Generation
{
    public sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessageDto> Messages,
        [property: JsonPropertyName("stream")] bool Stream = false
    );
}

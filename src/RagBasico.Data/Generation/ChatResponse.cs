using System.Text.Json.Serialization;

namespace RagBasico.Data.Generation
{
    public sealed record ChatResponse(
        [property: JsonPropertyName("message")] ChatMessageDto Message,
        [property: JsonPropertyName("done")] bool Done
    );
}

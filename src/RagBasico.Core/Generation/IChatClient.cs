namespace RagBasico.Core.Generation
{
    public interface IChatClient
    {
        Task<string> GetCompletionAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);
    }
}

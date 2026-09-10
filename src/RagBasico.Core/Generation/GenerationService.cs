using RagBasico.Core.Persistence;

namespace RagBasico.Core.Generation
{
    public sealed class GenerationService
    {
        private readonly IChatClient _chatClient;

        public GenerationService(IChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async Task<string> GenerateAsync(string question, IReadOnlyList<RetrievedChunk> context, CancellationToken ct = default)
        {
            var blocks = new List<string>();

            var systemPrompt = """
                                You are a helpful assistant.
                                Answer the user's question using only the provided context.
                                If the context does not contain enough information, say so.
                                Cite sources using the format [source:chunk_index].
                                """;

            foreach (var item in context)
            {
                var chunk = $"[{item.Source}:{item.ChunkIndex}] {item.Content}";
                blocks.Add(chunk);
            }

            var contextText = string.Join(
                Environment.NewLine,
                blocks
            );

            var userPrompt = $"""
                                Context:
                                {contextText}
                                Question:
                                {question}
                                """;

            var system = new ChatMessage("system", systemPrompt);
            var user = new ChatMessage("user", userPrompt);

            var prompt = new List<ChatMessage>
            {
                system,
                user
            };

            return await _chatClient.GetCompletionAsync(prompt, ct);
        }
    }
}

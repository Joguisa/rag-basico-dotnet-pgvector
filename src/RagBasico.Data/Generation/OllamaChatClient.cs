using RagBasico.Core.Generation;
using System.Net.Http.Json;

namespace RagBasico.Data.Generation
{
    public sealed class OllamaChatClient : IChatClient
    {
        private readonly HttpClient _httpClient;
        readonly string _generationModel;

        public OllamaChatClient(HttpClient httpClient, string generationModel)
        {
            _httpClient = httpClient;
            _generationModel = generationModel;
        }

        public async Task<string> GetCompletionAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
        {
            var dto = messages.Select(f => new ChatMessageDto(f.Role, f.Content)).ToList();

            var requestDto = new ChatRequest(_generationModel, dto);
            
            var response = await _httpClient.PostAsJsonAsync("api/chat", requestDto, ct);
            response.EnsureSuccessStatusCode();

            var responseDto = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);

            return responseDto?.Message?.Content ?? "";

        }
    }
}

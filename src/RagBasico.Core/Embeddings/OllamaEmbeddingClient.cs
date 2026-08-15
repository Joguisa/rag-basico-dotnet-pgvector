using System.Net.Http.Json;

namespace RagBasico.Core.Embeddings;

public sealed class OllamaEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    // mandar un batch de textos a POST /api/embed y devolver los vectores en el mismo orden de entrada. Solo sabe de Ollama
    public OllamaEmbeddingClient(HttpClient httpClient, string model)
    {
        _httpClient = httpClient;
        _model = model;
    }

    // "Dame una lista de textos." | "Te devolveré una lista de vectores." cada float[] es un embedding
    public async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default) // ct permite cancelar la operación asíncrona si ya no tiene sentido continuar.
    {

        var dtoRequest = new EmbeddingRequest(_model, texts);

        var response = await _httpClient.PostAsJsonAsync("api/embed", dtoRequest, ct);

        response.EnsureSuccessStatusCode(); // verifica que Ollama haya respondido correctamente.

        var responseDto = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);

        return responseDto?.Embeddings ?? [];
    }
}

using Microsoft.Extensions.Options;
using Npgsql;
using RagBasico.Api.Configuration;
using RagBasico.Api.DTOs;
using RagBasico.Core.Chunking;
using RagBasico.Core.Embeddings;
using RagBasico.Core.Ingestion;
using RagBasico.Core.Persistence;
using RagBasico.Data;
using RagBasico.Data.Embeddings;

var builder = WebApplication.CreateBuilder(args); // crea el constructor de la aplicacion web

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

builder.Services.AddSingleton(_ => RagDataSource.Create(connectionString)); 

builder.Services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>((sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IOptions<OllamaOptions>>().Value.BaseUrl;
    if (!string.IsNullOrEmpty(baseUrl))
        client.BaseAddress = new Uri(baseUrl);
});
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<IngestionOptions>(builder.Configuration.GetSection("Ingestion"));
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
builder.Services.AddScoped<Chunker>();

// No lo hacemos builder.Services.AddScoped<DocumentIngestionService>(); porque tenemos int batchSize y DI no sabe qué número poner ahí.
builder.Services.AddScoped<DocumentIngestionService>(sp => // sp significa: IServiceProvider "Dame una instancia de este servicio que ya registré."
{
    var batchSize = sp.GetRequiredService<IOptions<IngestionOptions>>().Value.BatchSize;
    var chunkSize = sp.GetRequiredService<IOptions<IngestionOptions>>().Value.ChunkSize;
    var overlap = sp.GetRequiredService<IOptions<IngestionOptions>>().Value.Overlap;
    var chunker = sp.GetRequiredService<Chunker>(); // Busca en DI una instancia de Chunker. Si no existe, lanza una excepción.
    var embeddingClient = sp.GetRequiredService<IEmbeddingClient>();
    var chunkRepository = sp.GetRequiredService<IChunkRepository>();

    return new DocumentIngestionService(chunker, embeddingClient, chunkRepository, batchSize, chunkSize, overlap);
});

var app = builder.Build(); // construye la app ya configurada

app.MapGet("/health", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
{
    var schemaReady = await SchemaCheck.ChunksTableExistsAsync(dataSource, ct);
    return schemaReady
        ? Results.Ok(new { status = "ok", schemaReady })
        : Results.Problem("chunks table not found", statusCode: 503);
});

app.MapPost("/ingest", async (
    IngestRequest request,
    DocumentIngestionService ingestionService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Source))
        return Results.BadRequest(new { error = "Source es requerido." });

    var chunksIngested = await ingestionService.IngestAsync(request.Source, request.Content, ct);
    return Results.Ok(new { source = request.Source, chunksIngested });
});

app.MapPost("/ingest/file", async (
    IngestFileRequest request,
    DocumentIngestionService ingestionService,
    IOptions<IngestionOptions> ingestionOptions,
    CancellationToken ct) =>
{
    // acá van los pasos 3, 4, 5
    var safeFileName = Path.GetFileName(request.FileName);

    var dataDirectory = ingestionOptions.Value.DataDirectory;
    var fullPath = Path.Combine(dataDirectory, safeFileName);
    if (!File.Exists(fullPath))
        return Results.NotFound(new { error = $"Archivo '{safeFileName}' no encontrado." });

    var content = await File.ReadAllTextAsync(fullPath, ct);

    var chunksIngested = await ingestionService.IngestAsync(safeFileName, content, ct);
    return Results.Ok(new { source = safeFileName, chunksIngested });
});

app.Run();


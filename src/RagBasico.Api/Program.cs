using Npgsql;
using RagBasico.Api.DTOs;
using RagBasico.Core.Embeddings;
using RagBasico.Core.Ingestion;
using RagBasico.Data;

var builder = WebApplication.CreateBuilder(args); // crea el constuctor de la aplicacion web

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

builder.Services.AddSingleton(_ => RagDataSource.Create(connectionString)); // 

builder.Services.AddHttpClient<OllamaEmbeddingClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
});

var app = builder.Build(); // construye la app ya configurada

app.MapGet("/health", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
{
    var schemaReady = await SchemaCheck.ChunksTableExistsAsync(dataSource, ct);
    return schemaReady
        ? Results.Ok(new { status = "ok", schemaReady })
        : Results.Problem("chunks table not found", statusCode: 503);
});

// nuevo endpoint en Program.cs, además del /health que ya existe
app.MapPost("/ingest", async (IngestRequest request, DocumentIngestionService ingestionService, CancellationToken ct) =>
{
    // llama a ingestionService.IngestAsync(...) y devuelve algo (a definir por vos: 200? cuántos chunks se guardaron?)
});



app.Run();


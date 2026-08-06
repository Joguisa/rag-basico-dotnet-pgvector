using Npgsql;
using RagBasico.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

builder.Services.AddSingleton(_ => RagDataSource.Create(connectionString));

var app = builder.Build();

app.MapGet("/health", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
{
    var schemaReady = await SchemaCheck.ChunksTableExistsAsync(dataSource, ct);
    return schemaReady
        ? Results.Ok(new { status = "ok", schemaReady })
        : Results.Problem("chunks table not found", statusCode: 503);
});

app.Run();

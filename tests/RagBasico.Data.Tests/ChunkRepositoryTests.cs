using Npgsql;
using RagBasico.Core.Persistence;

namespace RagBasico.Data.Tests;

// IClassFixture<PostgresFixture>: el contenedor Postgres se comparte entre todos los
// tests de esta clase (uno solo para toda la clase, no uno por test).
//
// IAsyncLifetime (en la propia clase, no en el fixture): xUnit crea una instancia nueva
// de ChunkRepositoryTests por cada [Fact], así que InitializeAsync corre antes de CADA
// test individual - lo usamos para vaciar la tabla y que un test no vea datos que dejó otro.
public sealed class ChunkRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ChunkRepository _repository;
    private readonly NpgsqlDataSource _dataSource;

    public ChunkRepositoryTests(PostgresFixture fixture)
    {
        _dataSource = fixture.DataSource;
        _repository = new ChunkRepository(_dataSource);
    }

    public async Task InitializeAsync()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE TABLE chunks RESTART IDENTITY", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Vector unitario en el plano (dim0, dim1); el resto de las 768 dimensiones queda en 0.
    // La distancia coseno de pgvector solo depende del angulo entre vectores, no de su
    // magnitud, asi que alcanza con 2 dimensiones "activas" para controlar esa distancia
    // de forma predecible: mismo angulo (1,0) vs (1,0) -> distancia 0; 45 grados (1,0) vs
    // (1,1) -> distancia ~0.293; 90 grados (1,0) vs (0,1) -> distancia 1.
    private static float[] BuildEmbedding(float dim0, float dim1)
    {
        var embedding = new float[768];
        embedding[0] = dim0;
        embedding[1] = dim1;
        return embedding;
    }

    [Fact]
    public async Task SearchAsync_DevuelveChunksOrdenadosPorDistanciaAscendente()
    {
        // Arrange
        var cercano = new StoredChunk("doc.md", 0, "chunk cercano", BuildEmbedding(1, 0));
        var medio = new StoredChunk("doc.md", 1, "chunk medio", BuildEmbedding(1, 1));
        var lejano = new StoredChunk("doc.md", 2, "chunk lejano", BuildEmbedding(0, 1));
        await _repository.InsertManyAsync([cercano, medio, lejano]);

        var query = BuildEmbedding(1, 0);

        // Act
        var resultados = await _repository.SearchAsync(query, topK: 3);

        // Assert
        Assert.Equal(3, resultados.Count);
        Assert.Equal(0, resultados[0].ChunkIndex);
        Assert.Equal(1, resultados[1].ChunkIndex);
        Assert.Equal(2, resultados[2].ChunkIndex);
        Assert.True(resultados[0].Distance < resultados[1].Distance);
        Assert.True(resultados[1].Distance < resultados[2].Distance);
    }

    [Fact]
    public async Task SearchAsync_RespetaTopK()
    {
        // Arrange
        var cercano = new StoredChunk("doc.md", 0, "chunk cercano", BuildEmbedding(1, 0));
        var medio = new StoredChunk("doc.md", 1, "chunk medio", BuildEmbedding(1, 1));
        var lejano = new StoredChunk("doc.md", 2, "chunk lejano", BuildEmbedding(0, 1));
        await _repository.InsertManyAsync([cercano, medio, lejano]);

        var query = BuildEmbedding(1, 0);

        // Act
        var resultados = await _repository.SearchAsync(query, topK: 2);

        // Assert
        Assert.Equal(2, resultados.Count);
        Assert.Equal(0, resultados[0].ChunkIndex);
        Assert.Equal(1, resultados[1].ChunkIndex);
    }

    [Fact]
    public async Task SearchAsync_VectorIdentico_TieneDistanciaCercanaACero()
    {
        // Arrange
        var embedding = BuildEmbedding(1, 0);
        var chunk = new StoredChunk("doc.md", 0, "chunk unico", embedding);
        await _repository.InsertManyAsync([chunk]);

        // Act
        var resultados = await _repository.SearchAsync(embedding, topK: 1);

        // Assert
        Assert.True(resultados[0].Distance < 0.0001f);
    }

    [Fact]
    public async Task InsertManyAsync_MismoSourceYChunkIndex_ActualizaContenidoEnVezDeDuplicar()
    {
        // Arrange
        var version1 = new StoredChunk("doc.md", 0, "version 1", BuildEmbedding(1, 0));
        var version2 = new StoredChunk("doc.md", 0, "version 2", BuildEmbedding(0, 1));

        // Act
        await _repository.InsertManyAsync([version1]);
        await _repository.InsertManyAsync([version2]);
        var resultados = await _repository.SearchAsync(BuildEmbedding(0, 1), topK: 10);

        // Assert: sigue habiendo un solo chunk (no se duplico), y quedo el contenido nuevo.
        Assert.Single(resultados);
        Assert.Equal("version 2", resultados[0].Content);
    }

    [Fact]
    public async Task DeleteOrphanedChunksAsync_BorraChunksConIndiceMayorOIgualAKeepCount()
    {
        // Arrange
        var chunk0 = new StoredChunk("doc.md", 0, "chunk 0", BuildEmbedding(1, 0));
        var chunk1 = new StoredChunk("doc.md", 1, "chunk 1", BuildEmbedding(1, 0));
        var chunk2 = new StoredChunk("doc.md", 2, "chunk 2", BuildEmbedding(1, 0));
        await _repository.InsertManyAsync([chunk0, chunk1, chunk2]);

        // Act
        await _repository.DeleteOrphanedChunksAsync("doc.md", keepCount: 2);
        var resultados = await _repository.SearchAsync(BuildEmbedding(1, 0), topK: 10);

        // Assert
        Assert.Equal(2, resultados.Count);

        var indicesRestantes = new List<int>();
        foreach (var resultado in resultados)
            indicesRestantes.Add(resultado.ChunkIndex);

        Assert.Contains(0, indicesRestantes);
        Assert.Contains(1, indicesRestantes);
        Assert.DoesNotContain(2, indicesRestantes);
    }
}

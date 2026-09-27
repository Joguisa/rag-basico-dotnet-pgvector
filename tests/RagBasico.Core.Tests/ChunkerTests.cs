using RagBasico.Core.Chunking;

namespace RagBasico.Core.Tests;

public class ChunkerTests
{
    // Bloque A: validaciones

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void Split_TextoVacio_DevuelveListaVacia(string text)
    {
        // Arrange
        var chunker = new Chunker();

        // Act
        var chunks = chunker.Split(text, 200, 50);

        // Assert
        Assert.Empty(chunks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Split_ChunkSizeInvalido_LanzaArgumentException(int chunkSize)
    {
        // Arrange
        var chunker = new Chunker();
        var text = "texto de prueba valido";
        var overlap = 50;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => chunker.Split(text, chunkSize, overlap));
        Assert.Equal("chunkSize", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void Split_OverlapNegativo_LanzaArgumentException(int overlap)
    {
        // Arrange
        var chunker = new Chunker();
        var text = "texto de prueba valido";
        var chunkSize = 400;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => chunker.Split(text, chunkSize, overlap));
        Assert.Equal("overlap", exception.ParamName);
    }

    [Theory]
    [InlineData(100)] // overlap == chunkSize
    [InlineData(150)] // overlap > chunkSize
    public void Split_OverlapMayorOIgualAChunkSize_LanzaArgumentException(int overlap)
    {
        // Arrange
        var chunker = new Chunker();
        var text = "texto de prueba valido";
        var chunkSize = 100;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => chunker.Split(text, chunkSize, overlap));
        Assert.Equal("overlap", exception.ParamName);
    }

    // Bloque B: comportamiento basico

    [Fact]
    public void Split_TextoMasCortoQueChunkSize_DevuelveUnSoloChunk()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "Hola mundo";

        // Act
        var chunks = chunker.Split(text, 200, 50);

        // Assert
        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal(text, chunks[0].Content);
    }

    [Fact]
    public void Split_TextoConEspaciosAlrededor_RecortaEspaciosDelContenido()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "   Hola mundo   \n";

        // Act
        var chunks = chunker.Split(text, 200, 50);

        // Assert
        Assert.Single(chunks);
        Assert.Equal("Hola mundo", chunks[0].Content);
    }

    [Fact]
    public void Split_VariosChunks_IndicesSonConsecutivos()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "Primer parrafo con contenido.\n\nSegundo parrafo con contenido.\n\nTercer parrafo con contenido.";

        // Act
        var chunks = chunker.Split(text, 35, 0);

        // Assert
        Assert.Equal(3, chunks.Count);
        for (int i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].Index);
    }

    // Bloque C: jerarquia de separadores

    [Fact]
    public void Split_ParrafosSeparadosPorDobleSaltoDeLinea_SePartenPorParrafo()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "Primer parrafo con contenido.\n\nSegundo parrafo con contenido.\n\nTercer parrafo con contenido.";

        // Act
        var chunks = chunker.Split(text, 35, 0);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("Primer parrafo con contenido.", chunks[0].Content);
        Assert.Equal("Segundo parrafo con contenido.", chunks[1].Content);
        Assert.Equal("Tercer parrafo con contenido.", chunks[2].Content);
    }

    [Fact]
    public void Split_SoloSaltosDeLineaSimples_SePartenPorLinea()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "Linea uno con texto\nLinea dos con texto\nLinea tres con texto";

        // Act
        var chunks = chunker.Split(text, 25, 0);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("Linea uno con texto", chunks[0].Content);
        Assert.Equal("Linea dos con texto", chunks[1].Content);
        Assert.Equal("Linea tres con texto", chunks[2].Content);
    }

    [Fact]
    public void Split_SoloPuntoSeguidoDeEspacio_SePartePorOracionConservandoElPunto()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "Primera oracion completa. Segunda oracion completa. Tercera oracion completa.";

        // Act
        var chunks = chunker.Split(text, 30, 0);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("Primera oracion completa.", chunks[0].Content);
        Assert.Equal("Segunda oracion completa.", chunks[1].Content);
        Assert.Equal("Tercera oracion completa.", chunks[2].Content);
    }

    [Fact]
    public void Split_SoloEspacios_SePartePorPalabra()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "uno dos tres cuatro cinco";

        // Act
        var chunks = chunker.Split(text, 6, 0);

        // Assert
        Assert.Equal(5, chunks.Count);
        Assert.Equal("uno", chunks[0].Content);
        Assert.Equal("dos", chunks[1].Content);
        Assert.Equal("tres", chunks[2].Content);
        Assert.Equal("cuatro", chunks[3].Content);
        Assert.Equal("cinco", chunks[4].Content);
    }

    [Fact]
    public void Split_TextoSinSeparadores_UsaHardSplitDeExactamenteChunkSize()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "abcdefghijklmnopqrstuvwxy"; // 25 caracteres, sin espacios/saltos/puntos

        // Act
        var chunks = chunker.Split(text, 10, 0);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal(10, chunks[0].Content.Length);
        Assert.Equal(10, chunks[1].Content.Length);
        Assert.Equal(5, chunks[2].Content.Length);

        var reconstruido = "";
        foreach (var chunk in chunks)
            reconstruido += chunk.Content;
        Assert.Equal(text, reconstruido);
    }

    // Bloque D: overlap

    [Fact]
    public void Split_OverlapCero_LosChunksNoComparteContenido()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "Primer parrafo con contenido.\n\nSegundo parrafo con contenido.\n\nTercer parrafo con contenido.";

        // Act
        var chunks = chunker.Split(text, 35, 0);

        // Assert: sin overlap, la suma de los chunks es igual a la suma de los parrafos originales
        // (si hubiera contenido duplicado por overlap, la suma seria mayor).
        var longitudTotal = 0;
        foreach (var chunk in chunks)
            longitudTotal += chunk.Content.Length;

        var longitudEsperada = "Primer parrafo con contenido.".Length
            + "Segundo parrafo con contenido.".Length
            + "Tercer parrafo con contenido.".Length;

        Assert.Equal(longitudEsperada, longitudTotal);
    }

    [Fact]
    public void Split_OverlapPositivo_FinalDeUnChunkApareceAlInicioDelSiguiente()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "alpha beta gamma delta";

        // Act
        var chunks = chunker.Split(text, 10, 3);

        // Assert
        Assert.Equal(3, chunks.Count);

        var overlapEsperado01 = chunks[0].Content.Substring(chunks[0].Content.Length - 3);
        Assert.StartsWith(overlapEsperado01, chunks[1].Content);

        var overlapEsperado12 = chunks[1].Content.Substring(chunks[1].Content.Length - 3);
        Assert.StartsWith(overlapEsperado12, chunks[2].Content);
    }

    [Fact]
    public void Split_TextoNormal_NingunChunkSuperaChunkSize()
    {
        // Arrange
        var chunker = new Chunker();
        var text = "uno dos tres cuatro cinco seis siete ocho nueve diez once doce trece catorce quince";
        var chunkSize = 25;

        // Act
        var chunks = chunker.Split(text, chunkSize, 5);

        // Assert
        foreach (var chunk in chunks)
            Assert.True(chunk.Content.Length <= chunkSize);
    }
}

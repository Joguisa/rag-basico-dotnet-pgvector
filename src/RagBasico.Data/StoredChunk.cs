namespace RagBasico.Data;

public sealed record StoredChunk(string Source, int ChunkIndex, string Content, float[] Embedding);

namespace RagBasico.Core.Persistence;

public sealed record StoredChunk(string Source, int ChunkIndex, string Content, float[] Embedding);

namespace RagBasico.Core.Persistence;

public sealed record RetrievedChunk(string Source, int ChunkIndex, string Content, float Distance);
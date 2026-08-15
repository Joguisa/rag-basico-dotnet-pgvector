namespace RagBasico.Core.Chunking;

// representa un fragmento de texto ya partido, todavia sin embedding
public sealed record TextChunk(int Index, string Content);

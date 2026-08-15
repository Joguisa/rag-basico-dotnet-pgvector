namespace RagBasico.Api.Configuration
{
    public class IngestionOptions
    {
        public int ChunkSize { get; set; }
        public int Overlap { get; set; }
        public int BatchSize { get; set; }

    }
}

using RagBasico.Core.Generation;
using RagBasico.Core.Persistence;
using RagBasico.Core.Retrieval;

namespace RagBasico.Core.Answering
{
    // orquestador
    public sealed class AnswerService
    {
        private readonly RetrievalService _retrievalService;
        private readonly GenerationService _generationService;

        public AnswerService(RetrievalService retrievalService, GenerationService generationService)
        {
            _retrievalService = retrievalService;
            _generationService = generationService;
        }

        public async Task<(string Answer, IReadOnlyList<RetrievedChunk> Citations)> AskAsync(string question, CancellationToken ct = default)
        {
            var chunks = await _retrievalService.SearchAsync(question, ct);
            
            var response = await _generationService.GenerateAsync(question, chunks, ct);

            return (response, chunks);
        }
    }
}

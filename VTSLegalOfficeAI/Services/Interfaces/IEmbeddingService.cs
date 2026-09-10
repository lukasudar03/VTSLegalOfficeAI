namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IEmbeddingService
    {
        Task<List<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
    }
}

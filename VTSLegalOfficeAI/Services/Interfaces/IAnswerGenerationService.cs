namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IAnswerGenerationService
    {
        Task<string> GenerateAnswerAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    }
}

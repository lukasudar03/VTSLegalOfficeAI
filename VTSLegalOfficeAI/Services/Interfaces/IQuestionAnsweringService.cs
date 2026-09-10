using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IQuestionAnsweringService
    {
        Task<AskAnswerResult> AskAsync(Guid documentId, Guid userId, string question, CancellationToken cancellationToken = default);
    }
}

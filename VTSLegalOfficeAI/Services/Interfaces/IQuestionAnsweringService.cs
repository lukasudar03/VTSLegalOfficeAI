using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IQuestionAnsweringService
    {
        Task<AskAnswerResult> AskAsync(Guid documentId, string question, CancellationToken cancellationToken = default);
    }
}

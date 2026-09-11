using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IQuestionAnsweringService
    {
        Task<AskAnswerResult> AskAsync(Guid documentId, Guid userId, string question, CancellationToken cancellationToken = default);
        Task<List<ChatMessage>> GetHistoryAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);
    }
}

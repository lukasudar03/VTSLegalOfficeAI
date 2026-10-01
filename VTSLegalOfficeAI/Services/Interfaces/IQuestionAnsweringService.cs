using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IQuestionAnsweringService
    {
        Task<AskAnswerResult> AskAsync(Guid documentId, Guid userId, string question, DateOnly? deadlineStartDate = null, CancellationToken cancellationToken = default);
        Task<List<ChatMessage>> GetHistoryAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);
        Task<AskAnswerResult> AskMultiAsync(Guid userId, string question, List<Guid>? documentIds, DateOnly? deadlineStartDate = null, CancellationToken cancellationToken = default);
        Task<List<ChatMessage>> GetMultiHistoryAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}

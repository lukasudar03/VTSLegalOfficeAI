using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IDocumentComparisonService
    {
        Task<DocumentComparisonResult> CompareAsync(Guid documentId1, Guid documentId2, Guid userId, CancellationToken cancellationToken = default);
    }
}

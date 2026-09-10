using Microsoft.AspNetCore.Http;
using VTSLegalOfficeAI.Entities;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadAsync(IFormFile file, Guid userId);
        Task<List<Document>> GetAllAsync(Guid userId);
        Task<Document?> GetByIdAsync(Guid id, Guid userId);
        Task ProcessDocumentAsync(Guid documentId, Guid userId);
    }
}

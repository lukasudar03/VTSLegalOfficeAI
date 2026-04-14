using Microsoft.AspNetCore.Http;
using VTSLegalOfficeAI.Entities;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadAsync(IFormFile file);
        Task<List<Document>> GetAllAsync();
        Task<Document?> GetByIdAsync(Guid id);
    }
}
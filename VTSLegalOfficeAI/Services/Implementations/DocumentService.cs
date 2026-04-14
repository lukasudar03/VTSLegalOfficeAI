using Microsoft.EntityFrameworkCore;
using VTSLegalOfficeAI.Data;
using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DocumentService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<Document> UploadAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File is required.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".pdf")
                throw new Exception("Only PDF files are allowed.");

            var uploadsFolder = Path.Combine(_environment.ContentRootPath, "UploadedFiles");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsFolder, storedFileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = new Document
            {
                Id = Guid.NewGuid(),
                FileName = file.FileName,
                StoredFileName = storedFileName,
                FilePath = fullPath,
                FileSizeBytes = file.Length,
                Status = "Uploaded",
                UploadedAt = DateTime.UtcNow
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            return document;
        }

        public async Task<List<Document>> GetAllAsync()
        {
            return await _context.Documents
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();
        }

        public async Task<Document?> GetByIdAsync(Guid id)
        {
            return await _context.Documents
                .FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
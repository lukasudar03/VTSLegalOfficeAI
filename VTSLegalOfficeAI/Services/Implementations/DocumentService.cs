using Microsoft.EntityFrameworkCore;
using Pgvector;
using VTSLegalOfficeAI.Data;
using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IPdfTextExtractorService _pdfTextExtractorService;
        private readonly ITextChunkingService _textChunkingService;
        private readonly IEmbeddingService _embeddingService;

        public DocumentService(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IPdfTextExtractorService pdfTextExtractorService,
            ITextChunkingService textChunkingService,
            IEmbeddingService embeddingService)
        {
            _context = context;
            _environment = environment;
            _pdfTextExtractorService = pdfTextExtractorService;
            _textChunkingService = textChunkingService;
            _embeddingService = embeddingService;
        }

        public async Task<Document> UploadAsync(IFormFile file, Guid userId)
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
                UserId = userId,
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

        public async Task<List<Document>> GetAllAsync(Guid userId)
        {
            return await _context.Documents
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UploadedAt)
                .ToListAsync();
        }

        public async Task<Document?> GetByIdAsync(Guid id, Guid userId)
        {
            return await _context.Documents
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        }

        public async Task ProcessDocumentAsync(Guid documentId, Guid userId)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(x => x.Id == documentId && x.UserId == userId);

            if (document == null)
                throw new Exception("Document not found.");

            if (!File.Exists(document.FilePath))
                throw new Exception("Physical file not found.");

            document.Status = "Processing";
            await _context.SaveChangesAsync();

            try
            {
                var existingChunks = await _context.DocumentChunks
                    .Where(c => c.DocumentId == documentId)
                    .ToListAsync();

                if (existingChunks.Count > 0)
                {
                    _context.DocumentChunks.RemoveRange(existingChunks);
                    await _context.SaveChangesAsync();
                }

                var extraction = _pdfTextExtractorService.ExtractText(document.FilePath);

                document.ExtractedText = extraction.Text;
                document.TotalPages = extraction.TotalPages;

                var textChunks = _textChunkingService.ChunkPages(extraction.Pages);

                if (textChunks.Count > 0)
                {
                    var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                        textChunks.Select(c => c.Content).ToList());

                    var documentChunks = textChunks.Select((chunk, i) => new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        ChunkIndex = chunk.ChunkIndex,
                        Content = chunk.Content,
                        PageFrom = chunk.PageFrom,
                        PageTo = chunk.PageTo,
                        Embedding = new Vector(embeddings[i]),
                        CreatedAt = DateTime.UtcNow
                    });

                    _context.DocumentChunks.AddRange(documentChunks);
                }

                document.Status = "Processed";
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Don't leave the document stuck on "Processing" with no way to retry from the UI.
                document.Status = "Uploaded";
                await _context.SaveChangesAsync();
                throw;
            }
        }

        public async Task DeleteDocumentAsync(Guid documentId, Guid userId)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(x => x.Id == documentId && x.UserId == userId);

            if (document == null)
                throw new Exception("Document not found.");

            if (File.Exists(document.FilePath))
                File.Delete(document.FilePath);

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }
    }
}

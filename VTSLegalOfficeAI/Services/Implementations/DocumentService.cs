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

        public async Task ProcessDocumentAsync(Guid documentId)
        {
            var document = await _context.Documents.FindAsync(documentId);

            if (document == null)
                throw new Exception("Document not found.");

            if (!File.Exists(document.FilePath))
                throw new Exception("Physical file not found.");

            document.Status = "Processing";
            await _context.SaveChangesAsync();

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
    }
}

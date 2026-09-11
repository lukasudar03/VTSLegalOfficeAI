using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using VTSLegalOfficeAI.Data;
using VTSLegalOfficeAI.Entities;
using VTSLegalOfficeAI.Services.Interfaces;
using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class QuestionAnsweringService : IQuestionAnsweringService
    {
        private const int TopK = 5;
        private const int HistoryLimit = 6;

        private readonly ApplicationDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly IAnswerGenerationService _answerGenerationService;

        public QuestionAnsweringService(
            ApplicationDbContext context,
            IEmbeddingService embeddingService,
            IAnswerGenerationService answerGenerationService)
        {
            _context = context;
            _embeddingService = embeddingService;
            _answerGenerationService = answerGenerationService;
        }

        public async Task<AskAnswerResult> AskAsync(Guid documentId, Guid userId, string question, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new Exception("Question is required.");

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);
            if (document == null)
                throw new Exception("Document not found.");

            if (document.Status != "Processed")
                throw new Exception("Document has not been processed yet.");

            var recentHistory = await _context.ChatMessages
                .Where(m => m.DocumentId == documentId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(HistoryLimit)
                .ToListAsync(cancellationToken);
            recentHistory.Reverse();

            var historyText = recentHistory.Count > 0
                ? string.Join("\n\n", recentHistory.Select(m => $"Pitanje: {m.Question}\nOdgovor: {m.Answer}"))
                : string.Empty;

            var searchQuestion = question;

            if (recentHistory.Count > 0)
            {
                var lastExchange = recentHistory[^1];
                searchQuestion = $"{lastExchange.Question} {lastExchange.Answer} {question}";
            }

            var questionEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { searchQuestion }, cancellationToken);
            var questionVector = new Vector(questionEmbeddings[0]);

            var topChunks = await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.Embedding.CosineDistance(questionVector))
                .Take(TopK)
                .ToListAsync(cancellationToken);

            if (topChunks.Count == 0)
                throw new Exception("Document has no processed chunks.");

            topChunks = topChunks.OrderBy(c => c.ChunkIndex).ToList();

            var contextText = string.Join(
                "\n\n",
                topChunks.Select(c => $"[Strane {c.PageFrom}-{c.PageTo}]\n{c.Content}"));

            const string systemPrompt =
                "Ti si asistent koji odgovara na pitanja isključivo na osnovu datog konteksta iz dokumenta. " +
                "Ako je dat prethodni razgovor, koristi ga samo da razumeš na šta se novo pitanje odnosi (npr. zamenice " +
                "poput \"to\" ili \"taj deo\"), ali odgovor zasnivaj isključivo na kontekstu iz dokumenta. " +
                "Ako odgovor ne postoji u kontekstu, jasno reci da informacija nije pronađena u dokumentu. " +
                "Kada je moguće, referenciraj broj strane iz konteksta.";

            var historyBlock = recentHistory.Count > 0
                ? $"Prethodni razgovor:\n{historyText}\n\n"
                : string.Empty;

            var userPrompt = $"{historyBlock}Kontekst iz dokumenta:\n{contextText}\n\nPitanje: {question}";

            var answer = await _answerGenerationService.GenerateAnswerAsync(systemPrompt, userPrompt, cancellationToken);

            var sources = topChunks
                .Select(c => new ChunkSource(c.Id, c.ChunkIndex, c.PageFrom, c.PageTo, c.Content))
                .ToList();

            var storedSources = sources.Select(s => new
            {
                chunkId = s.ChunkId,
                chunkIndex = s.ChunkIndex,
                pageFrom = s.PageFrom,
                pageTo = s.PageTo,
                excerpt = s.Content.Length > 300 ? s.Content[..300] + "…" : s.Content
            });

            var chatMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Question = question,
                Answer = answer,
                SourcesJson = JsonSerializer.Serialize(storedSources),
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync(cancellationToken);

            return new AskAnswerResult(chatMessage.Id, answer, sources, chatMessage.CreatedAt);
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default)
        {
            var documentExists = await _context.Documents
                .AnyAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);
            if (!documentExists)
                throw new Exception("Document not found.");

            return await _context.ChatMessages
                .Where(m => m.DocumentId == documentId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}

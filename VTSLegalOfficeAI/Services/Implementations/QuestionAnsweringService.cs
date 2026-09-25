using System.Text.Json;
using System.Text.RegularExpressions;
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

        private const string SingleDocumentSystemPrompt =
            "Ti si asistent koji odgovara na pitanja isključivo na osnovu datog konteksta iz dokumenta. " +
            "Ako je dat prethodni razgovor, koristi ga samo da razumeš na šta se novo pitanje odnosi (npr. zamenice " +
            "poput \"to\" ili \"taj deo\"), ali odgovor zasnivaj isključivo na kontekstu iz dokumenta. " +
            "Nemoj navoditi brojeve članova, stavova ili tačaka koji se ne pojavljuju doslovno u datom kontekstu. " +
            "Ako odgovor ne postoji u kontekstu, jasno i kratko reci da informacija nije pronađena u dokumentu, " +
            "umesto da nagađaš ili izmišljaš sadržaj. Kada je moguće, referenciraj broj strane iz konteksta. " +
            ConfidenceInstruction;

        private const string MultiDocumentSystemPrompt =
            "Ti si asistent koji odgovara na pitanja isključivo na osnovu datog konteksta iz priloženih dokumenata. " +
            "Kontekst dolazi iz više dokumenata, od kojih je svaki označen nazivom i tipom akta (Zakon ili Pravilnik). " +
            "Za svaki deo odgovora jasno navedi iz kog dokumenta i tipa akta potiče. " +
            "Ako se dva dokumenta razlikuju u vezi sa istom temom, jasno to naznači i upozori da zakon ima veću " +
            "pravnu snagu od pravilnika, uz napomenu da je ovo samo gruba naznaka koju treba proveriti kod nadležnog " +
            "lica. Ako je dat prethodni razgovor, koristi ga samo da razumeš na šta se novo pitanje odnosi, ali " +
            "odgovor zasnivaj isključivo na priloženom kontekstu. Nemoj navoditi brojeve članova, stavova ili tačaka " +
            "koji se ne pojavljuju doslovno u datom kontekstu. Ako odgovor ne postoji u kontekstu, jasno i kratko " +
            "reci da informacija nije pronađena u priloženim dokumentima, umesto da nagađaš ili izmišljaš sadržaj. " +
            ConfidenceInstruction;

        private const string ConfidenceInstruction =
            "Na samom kraju odgovora, u posebnom redu, napiši tačno u ovom formatu i ni na koji način ga ne menjaj: " +
            "\"POUZDANOST: NIVO - obrazloženje\", gde je NIVO tačno jedna od reči NISKA, SREDNJA ili VISOKA. " +
            "Proceni NIVO na osnovu toga koliko dati kontekst direktno i nedvosmisleno odgovara na pitanje. " +
            "Koristi NISKA kada kontekst samo delimično ili posredno pokriva pitanje, kada moraš da nagađaš deo " +
            "odgovora, ili kada odgovor uopšte nije pronađen. Kada je NIVO nizak, u samom tekstu odgovora (pre ove " +
            "poslednje linije) jasno napiši da nisi siguran i da to treba proveriti kod nadležnog lica.";

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

            var baseQuery = _context.DocumentChunks.Where(c => c.DocumentId == documentId);
            var topChunks = await SearchChunksAsync(baseQuery, question, recentHistory, cancellationToken);

            if (topChunks.Count == 0)
                throw new Exception("Document has no processed chunks.");

            topChunks = topChunks.OrderBy(c => c.ChunkIndex).ToList();

            var historyBlock = BuildHistoryBlock(recentHistory);
            var contextText = BuildContextText(topChunks, includeDocumentInfo: false);
            var userPrompt = $"{historyBlock}Kontekst iz dokumenta:\n{contextText}\n\nPitanje: {question}";

            var rawAnswer = await _answerGenerationService.GenerateAnswerAsync(SingleDocumentSystemPrompt, userPrompt, cancellationToken);
            var (answer, confidence, confidenceNote) = ExtractConfidence(rawAnswer);

            var sources = topChunks
                .Select(c => new ChunkSource(c.Id, c.ChunkIndex, c.PageFrom, c.PageTo, c.Content, document.Id, document.FileName, document.DocumentType))
                .ToList();

            var chatMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                UserId = userId,
                Question = question,
                Answer = answer,
                SourcesJson = JsonSerializer.Serialize(BuildStoredSources(sources)),
                Confidence = confidence,
                ConfidenceNote = confidenceNote,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync(cancellationToken);

            return new AskAnswerResult(chatMessage.Id, answer, sources, chatMessage.CreatedAt, confidence, confidenceNote);
        }

        public async Task<AskAnswerResult> AskMultiAsync(Guid userId, string question, List<Guid>? documentIds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new Exception("Question is required.");

            var hasDocuments = documentIds is { Count: > 0 }
                ? await _context.Documents.AnyAsync(d => d.UserId == userId && d.Status == "Processed" && documentIds.Contains(d.Id), cancellationToken)
                : await _context.Documents.AnyAsync(d => d.UserId == userId && d.Status == "Processed", cancellationToken);

            if (!hasDocuments)
                throw new Exception("No processed documents available to search.");

            var recentHistory = await _context.ChatMessages
                .Where(m => m.UserId == userId && m.DocumentId == null)
                .OrderByDescending(m => m.CreatedAt)
                .Take(HistoryLimit)
                .ToListAsync(cancellationToken);
            recentHistory.Reverse();

            var baseQuery = _context.DocumentChunks
                .Include(c => c.Document)
                .Where(c => c.Document.UserId == userId && c.Document.Status == "Processed");

            if (documentIds is { Count: > 0 })
                baseQuery = baseQuery.Where(c => documentIds.Contains(c.DocumentId));

            var topChunks = await SearchChunksAsync(baseQuery, question, recentHistory, cancellationToken);

            if (topChunks.Count == 0)
                throw new Exception("No processed chunks available to search.");

            topChunks = topChunks.OrderBy(c => c.Document.FileName).ThenBy(c => c.ChunkIndex).ToList();

            var historyBlock = BuildHistoryBlock(recentHistory);
            var contextText = BuildContextText(topChunks, includeDocumentInfo: true);
            var userPrompt = $"{historyBlock}Kontekst iz priloženih dokumenata:\n{contextText}\n\nPitanje: {question}";

            var rawAnswer = await _answerGenerationService.GenerateAnswerAsync(MultiDocumentSystemPrompt, userPrompt, cancellationToken);
            var (answer, confidence, confidenceNote) = ExtractConfidence(rawAnswer);

            var sources = topChunks
                .Select(c => new ChunkSource(c.Id, c.ChunkIndex, c.PageFrom, c.PageTo, c.Content, c.DocumentId, c.Document.FileName, c.Document.DocumentType))
                .ToList();

            var chatMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                DocumentId = null,
                UserId = userId,
                Question = question,
                Answer = answer,
                SourcesJson = JsonSerializer.Serialize(BuildStoredSources(sources)),
                Confidence = confidence,
                ConfidenceNote = confidenceNote,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync(cancellationToken);

            return new AskAnswerResult(chatMessage.Id, answer, sources, chatMessage.CreatedAt, confidence, confidenceNote);
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

        public async Task<List<ChatMessage>> GetMultiHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.ChatMessages
                .Where(m => m.UserId == userId && m.DocumentId == null)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        private async Task<List<DocumentChunk>> SearchChunksAsync(
            IQueryable<DocumentChunk> baseQuery,
            string question,
            List<ChatMessage> recentHistory,
            CancellationToken cancellationToken)
        {
            if (recentHistory.Count > 0)
            {
                var lastExchange = recentHistory[^1];
                var searchQuestion = $"{lastExchange.Question} {lastExchange.Answer} {question}";

                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                    new[] { question, searchQuestion }, cancellationToken);
                var directVector = new Vector(embeddings[0]);
                var contextualVector = new Vector(embeddings[1]);

                var directChunks = await baseQuery
                    .OrderBy(c => c.Embedding.CosineDistance(directVector))
                    .Take(TopK)
                    .ToListAsync(cancellationToken);

                var contextualChunks = await baseQuery
                    .OrderBy(c => c.Embedding.CosineDistance(contextualVector))
                    .Take(TopK)
                    .ToListAsync(cancellationToken);

                return directChunks
                    .Concat(contextualChunks)
                    .GroupBy(c => c.Id)
                    .Select(g => g.First())
                    .Take(TopK + 3)
                    .ToList();
            }

            var questionEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { question }, cancellationToken);
            var questionVector = new Vector(questionEmbeddings[0]);

            return await baseQuery
                .OrderBy(c => c.Embedding.CosineDistance(questionVector))
                .Take(TopK)
                .ToListAsync(cancellationToken);
        }

        private static readonly Regex ConfidenceRegex = new(
            @"POUZDANOST:\s*(NISKA|SREDNJA|VISOKA)\s*-?\s*(.*)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private static (string Answer, string Confidence, string ConfidenceNote) ExtractConfidence(string rawAnswer)
        {
            var match = ConfidenceRegex.Match(rawAnswer);
            if (!match.Success)
                return (rawAnswer.Trim(), "SREDNJA", string.Empty);

            var confidence = match.Groups[1].Value.ToUpperInvariant();
            var note = match.Groups[2].Value.Trim();
            var cleanAnswer = rawAnswer[..match.Index].TrimEnd();

            return (cleanAnswer.Length > 0 ? cleanAnswer : rawAnswer.Trim(), confidence, note);
        }

        private static string BuildHistoryBlock(List<ChatMessage> recentHistory)
        {
            if (recentHistory.Count == 0)
                return string.Empty;

            var historyText = string.Join(
                "\n\n",
                recentHistory.Select(m => $"Pitanje: {m.Question}\nOdgovor: {m.Answer}"));

            return $"Prethodni razgovor:\n{historyText}\n\n";
        }

        private static string BuildContextText(List<DocumentChunk> chunks, bool includeDocumentInfo)
        {
            return string.Join(
                "\n\n",
                chunks.Select(c => includeDocumentInfo
                    ? $"[{c.Document.DocumentType}: {c.Document.FileName}, strane {c.PageFrom}-{c.PageTo}]\n{c.Content}"
                    : $"[Strane {c.PageFrom}-{c.PageTo}]\n{c.Content}"));
        }

        private static IEnumerable<object> BuildStoredSources(List<ChunkSource> sources)
        {
            return sources.Select(s => new
            {
                chunkId = s.ChunkId,
                chunkIndex = s.ChunkIndex,
                pageFrom = s.PageFrom,
                pageTo = s.PageTo,
                excerpt = s.Content.Length > 300 ? s.Content[..300] + "…" : s.Content,
                documentId = s.DocumentId,
                fileName = s.FileName,
                documentType = s.DocumentType
            });
        }
    }
}

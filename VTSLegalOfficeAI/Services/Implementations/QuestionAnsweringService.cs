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
            DeadlineInstruction +
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
            DeadlineInstruction +
            ConfidenceInstruction;

        private const string DeadlineInstruction =
            "Ako odredba iz konteksta koja je relevantna za pitanje navodi konkretan rok (vremenski period u " +
            "danima, mesecima ili godinama), na kraju odgovora, u posebnom redu PRE linije koja počinje sa " +
            "\"POUZDANOST:\", napiši tačno u ovom formatu: \"ROK: N JEDINICA\" (npr. \"ROK: 15 dana\"), gde je N " +
            "ceo broj, a JEDINICA jedna od reči dana, meseci ili godina, doslovno onako kako piše u kontekstu. " +
            "Ako pitanje ne traži ili kontekst ne navodi nijedan konkretan rok, napiši \"ROK: NIJE PRONAĐEN\". ";

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

        public async Task<AskAnswerResult> AskAsync(Guid documentId, Guid userId, string question, DateOnly? deadlineStartDate = null, CancellationToken cancellationToken = default)
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
            var (searchedChunks, bestDistance, directDistance) = await SearchChunksAsync(baseQuery, question, recentHistory, cancellationToken);
            var topChunks = searchedChunks;

            if (topChunks.Count == 0)
                throw new Exception("Document has no processed chunks.");

            topChunks = topChunks.OrderBy(c => c.ChunkIndex).ToList();

            var historyBlock = BuildHistoryBlock(recentHistory);
            var contextText = BuildContextText(topChunks, includeDocumentInfo: false);
            var userPrompt = $"{historyBlock}Kontekst iz dokumenta:\n{contextText}\n\nPitanje: {question}";

            var rawAnswer = await _answerGenerationService.GenerateAnswerAsync(SingleDocumentSystemPrompt, userPrompt, cancellationToken);
            var (deadlineAmount, deadlineUnit, afterDeadlineAnswer) = ExtractDeadline(rawAnswer);
            var (answer, confidence, confidenceNote) = ExtractConfidence(afterDeadlineAnswer, bestDistance, directDistance);
            var deadlineDueDate = ComputeDeadlineDueDate(deadlineStartDate, deadlineAmount, deadlineUnit);

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
                DeadlineAmount = deadlineAmount,
                DeadlineUnit = deadlineUnit,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync(cancellationToken);

            return new AskAnswerResult(chatMessage.Id, answer, sources, chatMessage.CreatedAt, confidence, confidenceNote, deadlineAmount, deadlineUnit, deadlineDueDate);
        }

        public async Task<AskAnswerResult> AskMultiAsync(Guid userId, string question, List<Guid>? documentIds, DateOnly? deadlineStartDate = null, CancellationToken cancellationToken = default)
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

            var (searchedChunks, bestDistance, directDistance) = await SearchChunksAsync(baseQuery, question, recentHistory, cancellationToken);
            var topChunks = searchedChunks;

            if (topChunks.Count == 0)
                throw new Exception("No processed chunks available to search.");

            topChunks = topChunks.OrderBy(c => c.Document.FileName).ThenBy(c => c.ChunkIndex).ToList();

            var historyBlock = BuildHistoryBlock(recentHistory);
            var contextText = BuildContextText(topChunks, includeDocumentInfo: true);
            var userPrompt = $"{historyBlock}Kontekst iz priloženih dokumenata:\n{contextText}\n\nPitanje: {question}";

            var rawAnswer = await _answerGenerationService.GenerateAnswerAsync(MultiDocumentSystemPrompt, userPrompt, cancellationToken);
            var (deadlineAmount, deadlineUnit, afterDeadlineAnswer) = ExtractDeadline(rawAnswer);
            var (answer, confidence, confidenceNote) = ExtractConfidence(afterDeadlineAnswer, bestDistance, directDistance);
            var deadlineDueDate = ComputeDeadlineDueDate(deadlineStartDate, deadlineAmount, deadlineUnit);

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
                DeadlineAmount = deadlineAmount,
                DeadlineUnit = deadlineUnit,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync(cancellationToken);

            return new AskAnswerResult(chatMessage.Id, answer, sources, chatMessage.CreatedAt, confidence, confidenceNote, deadlineAmount, deadlineUnit, deadlineDueDate);
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

        private const double LowRelevanceDistanceThreshold = 0.27;
        private const double HighRelevanceDistanceThreshold = 0.20;

        private async Task<(List<DocumentChunk> Chunks, double BestDistance, double DirectDistance)> SearchChunksAsync(
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

                var directRanked = await baseQuery
                    .Select(c => new { Chunk = c, Distance = c.Embedding.CosineDistance(directVector) })
                    .OrderBy(x => x.Distance)
                    .Take(TopK)
                    .ToListAsync(cancellationToken);

                var contextualRanked = await baseQuery
                    .Select(c => new { Chunk = c, Distance = c.Embedding.CosineDistance(contextualVector) })
                    .OrderBy(x => x.Distance)
                    .Take(TopK)
                    .ToListAsync(cancellationToken);

                var merged = directRanked
                    .Concat(contextualRanked)
                    .GroupBy(x => x.Chunk.Id)
                    .Select(g => g.OrderBy(x => x.Distance).First())
                    .OrderBy(x => x.Distance)
                    .Take(TopK + 3)
                    .ToList();

                var bestOfDirect = directRanked.Count > 0 ? directRanked.Min(x => x.Distance) : 1d;
                var bestOfContextual = contextualRanked.Count > 0 ? contextualRanked.Min(x => x.Distance) : 1d;
                return (merged.Select(x => x.Chunk).ToList(), Math.Min(bestOfDirect, bestOfContextual), bestOfDirect);
            }

            var questionEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { question }, cancellationToken);
            var questionVector = new Vector(questionEmbeddings[0]);

            var ranked = await baseQuery
                .Select(c => new { Chunk = c, Distance = c.Embedding.CosineDistance(questionVector) })
                .OrderBy(x => x.Distance)
                .Take(TopK)
                .ToListAsync(cancellationToken);

            var best = ranked.Count > 0 ? ranked.Min(x => x.Distance) : 1d;
            return (ranked.Select(x => x.Chunk).ToList(), best, best);
        }

        private static readonly Regex ConfidenceRegex = new(
            @"POUZDANOST:\s*(NISKA|SREDNJA|VISOKA)\s*-?\s*(.*)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex DeadlineRegex = new(
            @"^[ \t]*ROK:[ \t]*(?:(?<amount>\d+)[ \t]*(?<unit>dana|dan|meseci|mesec|meseca|godina|godine|godinu)|NIJE PRONAĐEN)[ \t]*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex InlineDeadlineRegex = new(
            @"u\s+roku\s+od\s+(?<amount>\d+)[ \t]*(?:\([^)]*\))?[ \t]*(?<unit>dana|dan|meseci|mesec|meseca|godina|godine|godinu)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static (int? Amount, string? Unit, string Answer) ExtractDeadline(string rawAnswer)
        {
            var match = DeadlineRegex.Match(rawAnswer);
            if (match.Success)
            {
                int? amount = match.Groups["amount"].Success ? int.Parse(match.Groups["amount"].Value) : null;
                string? unit = match.Groups["unit"].Success ? NormalizeDeadlineUnit(match.Groups["unit"].Value) : null;

                var cleaned = DeadlineRegex.Replace(rawAnswer, string.Empty, 1);
                cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n").Trim();

                return (amount, unit, cleaned.Length > 0 ? cleaned : rawAnswer.Trim());
            }

            var inlineMatch = InlineDeadlineRegex.Match(rawAnswer);
            if (inlineMatch.Success)
            {
                var amount = int.Parse(inlineMatch.Groups["amount"].Value);
                var unit = NormalizeDeadlineUnit(inlineMatch.Groups["unit"].Value);
                return (amount, unit, rawAnswer);
            }

            return (null, null, rawAnswer);
        }

        private static string NormalizeDeadlineUnit(string raw)
        {
            var lower = raw.ToLowerInvariant();
            if (lower.StartsWith("dan"))
                return "dana";
            if (lower.StartsWith("mesec"))
                return "meseci";
            if (lower.StartsWith("godin"))
                return "godina";
            return lower;
        }

        private static DateOnly? ComputeDeadlineDueDate(DateOnly? startDate, int? amount, string? unit)
        {
            if (startDate == null || amount == null || unit == null)
                return null;

            return unit switch
            {
                "dana" => startDate.Value.AddDays(amount.Value),
                "meseci" => startDate.Value.AddMonths(amount.Value),
                "godina" => startDate.Value.AddYears(amount.Value),
                _ => null,
            };
        }

        private static readonly string[] NotFoundPhrases =
        {
            "nije pronađen", "nije pronađena", "nije pronađeno", "ne postoji u",
            "ne sadrži informacij", "ne možemo odgovoriti", "ne može odgovoriti",
            "nemam informacij", "nije navedeno u", "ne spominje", "ne pominje",
            "nije definisan", "nije regulisan", "informacija nije",
            "nije povezano", "izvan konteksta", "izvan sadržaja",
            "nisam našao", "nismo našli", "nisam pronašao", "nisam mogao da pronađem"
        };

        private static (string Answer, string Confidence, string ConfidenceNote) ExtractConfidence(string rawAnswer, double bestDistance, double directDistance)
        {
            var match = ConfidenceRegex.Match(rawAnswer);

            string cleanAnswer;
            string confidence;
            string note;

            if (match.Success)
            {
                confidence = match.Groups[1].Value.ToUpperInvariant();
                note = match.Groups[2].Value.Trim();
                var stripped = rawAnswer[..match.Index].TrimEnd();
                cleanAnswer = stripped.Length > 0 ? stripped : rawAnswer.Trim();
            }
            else
            {
                cleanAnswer = rawAnswer.Trim();
                confidence = "SREDNJA";
                note = string.Empty;
            }

            var lowerAnswer = cleanAnswer.ToLowerInvariant();
            if (confidence != "NISKA" && NotFoundPhrases.Any(p => lowerAnswer.Contains(p)))
            {
                confidence = "NISKA";
                if (string.IsNullOrEmpty(note))
                    note = "Odgovor ukazuje da tražena informacija nije pronađena u priloženim dokumentima.";
            }

            if (confidence != "NISKA" && bestDistance > LowRelevanceDistanceThreshold)
            {
                confidence = "NISKA";
                if (string.IsNullOrEmpty(note))
                    note = "Ni najsličniji pronađeni odlomak nije dovoljno blizak pitanju — moguće je da pitanje izlazi izvan sadržaja priloženih dokumenata.";
            }
            else if (confidence == "SREDNJA" && directDistance < HighRelevanceDistanceThreshold)
            {
                confidence = "VISOKA";
                if (string.IsNullOrEmpty(note))
                    note = "Najsličniji pronađeni odlomak je vrlo blizak pitanju.";
            }

            return (cleanAnswer, confidence, note);
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

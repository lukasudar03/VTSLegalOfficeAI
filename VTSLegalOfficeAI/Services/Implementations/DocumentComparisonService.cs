using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VTSLegalOfficeAI.Data;
using VTSLegalOfficeAI.Services.Interfaces;
using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class DocumentComparisonService : IDocumentComparisonService
    {
        private const int MaxSummarizedChanges = 8;

        private static readonly Regex ArticleHeadingRegex = new(
            @"[Č■]lan\s+(\d{1,4})\.?",
            RegexOptions.Compiled);

        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        private const string ComparisonSystemPrompt =
            "Upoređuješ dve verzije istog člana pravnog akta. U jednoj do dve rečenice, na srpskom, opiši šta je " +
            "suštinski promenjeno između stare i nove verzije (npr. promenjen rok, dodat uslov, izmenjen iznos). " +
            "Ako ne možeš da utvrdiš suštinsku razliku, reci da je razlika neznatna ili formalna (npr. interpunkcija). " +
            "Ne izmišljaj izmene koje ne vidiš u tekstu. Odgovori samo sažetkom, bez uvoda i bez citiranja celog teksta.";

        private readonly ApplicationDbContext _context;
        private readonly IAnswerGenerationService _answerGenerationService;

        public DocumentComparisonService(ApplicationDbContext context, IAnswerGenerationService answerGenerationService)
        {
            _context = context;
            _answerGenerationService = answerGenerationService;
        }

        public async Task<DocumentComparisonResult> CompareAsync(Guid documentId1, Guid documentId2, Guid userId, CancellationToken cancellationToken = default)
        {
            if (documentId1 == documentId2)
                throw new Exception("Izaberi dva različita dokumenta za poređenje.");

            var document1 = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId1 && d.UserId == userId, cancellationToken);
            var document2 = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId2 && d.UserId == userId, cancellationToken);

            if (document1 == null || document2 == null)
                throw new Exception("Document not found.");

            if (string.IsNullOrWhiteSpace(document1.ExtractedText) || string.IsNullOrWhiteSpace(document2.ExtractedText))
                throw new Exception("Oba dokumenta moraju biti obrađena pre poređenja.");

            var articles1 = ParseArticles(document1.ExtractedText);
            var articles2 = ParseArticles(document2.ExtractedText);

            var numbers1 = articles1.Keys.ToHashSet();
            var numbers2 = articles2.Keys.ToHashSet();

            var addedNumbers = numbers2.Except(numbers1).OrderBy(n => n).ToList();
            var removedNumbers = numbers1.Except(numbers2).OrderBy(n => n).ToList();
            var commonNumbers = numbers1.Intersect(numbers2).OrderBy(n => n).ToList();

            var changedNumbers = new List<int>();
            var unchangedCount = 0;

            foreach (var number in commonNumbers)
            {
                if (Normalize(articles1[number]) == Normalize(articles2[number]))
                    unchangedCount++;
                else
                    changedNumbers.Add(number);
            }

            var added = addedNumbers.Select(n => new ArticleDiff(n, null, articles2[n], null)).ToList();
            var removed = removedNumbers.Select(n => new ArticleDiff(n, articles1[n], null, null)).ToList();

            var changed = new List<ArticleDiff>();
            foreach (var number in changedNumbers)
            {
                string? summary = null;
                if (changed.Count < MaxSummarizedChanges)
                {
                    summary = await SummarizeChangeAsync(articles1[number], articles2[number], cancellationToken);
                }

                changed.Add(new ArticleDiff(number, articles1[number], articles2[number], summary));
            }

            return new DocumentComparisonResult(
                document1.FileName,
                document2.FileName,
                unchangedCount,
                added,
                removed,
                changed);
        }

        private async Task<string> SummarizeChangeAsync(string oldText, string newText, CancellationToken cancellationToken)
        {
            var userPrompt = $"Stara verzija:\n{oldText}\n\nNova verzija:\n{newText}";
            return await _answerGenerationService.GenerateAnswerAsync(ComparisonSystemPrompt, userPrompt, cancellationToken);
        }

        private static Dictionary<int, string> ParseArticles(string text)
        {
            var matches = ArticleHeadingRegex.Matches(text);
            var articles = new Dictionary<int, string>();

            for (var i = 0; i < matches.Count; i++)
            {
                if (!int.TryParse(matches[i].Groups[1].Value, out var number))
                    continue;

                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var articleText = text[start..end].Trim();

                if (!articles.ContainsKey(number))
                    articles[number] = articleText;
            }

            return articles;
        }

        private static string Normalize(string text) => WhitespaceRegex.Replace(text, " ").Trim();
    }
}

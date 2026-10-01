namespace VTSLegalOfficeAI.Services.Models
{
    public record ArticleDiff(int ArticleNumber, string? OldText, string? NewText, string? Summary);

    public record DocumentComparisonResult(
        string Document1Name,
        string Document2Name,
        int UnchangedCount,
        List<ArticleDiff> Added,
        List<ArticleDiff> Removed,
        List<ArticleDiff> Changed);
}

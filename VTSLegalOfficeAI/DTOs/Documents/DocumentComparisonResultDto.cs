namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class ArticleDiffDto
    {
        public int ArticleNumber { get; set; }
        public string? OldText { get; set; }
        public string? NewText { get; set; }
        public string? Summary { get; set; }
    }

    public class DocumentComparisonResultDto
    {
        public string Document1Name { get; set; } = string.Empty;
        public string Document2Name { get; set; } = string.Empty;
        public int UnchangedCount { get; set; }
        public List<ArticleDiffDto> Added { get; set; } = new();
        public List<ArticleDiffDto> Removed { get; set; } = new();
        public List<ArticleDiffDto> Changed { get; set; } = new();
    }
}

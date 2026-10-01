namespace VTSLegalOfficeAI.Services.Models
{
    public record ChunkSource(
        Guid ChunkId,
        int ChunkIndex,
        int PageFrom,
        int PageTo,
        string Content,
        Guid DocumentId,
        string FileName,
        string DocumentType,
        bool IsRelatedProvision = false);

    public record AskAnswerResult(
        Guid Id,
        string Answer,
        List<ChunkSource> Sources,
        DateTime CreatedAt,
        string Confidence,
        string ConfidenceNote);
}

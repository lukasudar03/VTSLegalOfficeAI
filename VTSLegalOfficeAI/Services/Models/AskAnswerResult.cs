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
        string DocumentType);

    public record AskAnswerResult(Guid Id, string Answer, List<ChunkSource> Sources, DateTime CreatedAt);
}

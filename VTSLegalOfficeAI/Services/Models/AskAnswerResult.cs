namespace VTSLegalOfficeAI.Services.Models
{
    public record ChunkSource(Guid ChunkId, int ChunkIndex, int PageFrom, int PageTo, string Content);

    public record AskAnswerResult(Guid Id, string Answer, List<ChunkSource> Sources, DateTime CreatedAt);
}

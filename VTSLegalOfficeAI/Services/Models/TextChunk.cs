namespace VTSLegalOfficeAI.Services.Models
{
    public record TextChunk(int ChunkIndex, string Content, int PageFrom, int PageTo);
}

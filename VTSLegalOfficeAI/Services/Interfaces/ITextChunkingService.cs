using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface ITextChunkingService
    {
        List<TextChunk> ChunkPages(IReadOnlyList<PdfPageText> pages);
    }
}

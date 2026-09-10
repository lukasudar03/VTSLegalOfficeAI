using System.Text;
using VTSLegalOfficeAI.Services.Interfaces;
using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class TextChunkingService : ITextChunkingService
    {
        private const int ChunkSizeChars = 1200;
        private const int OverlapChars = 200;

        public List<TextChunk> ChunkPages(IReadOnlyList<PdfPageText> pages)
        {
            var chunks = new List<TextChunk>();
            var buffer = new StringBuilder();
            int chunkIndex = 0;
            int? bufferPageFrom = null;
            int bufferPageTo = 0;

            foreach (var page in pages)
            {
                var words = page.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    if (buffer.Length > 0)
                        buffer.Append(' ');
                    buffer.Append(word);

                    bufferPageFrom ??= page.PageNumber;
                    bufferPageTo = page.PageNumber;

                    if (buffer.Length >= ChunkSizeChars)
                    {
                        var content = buffer.ToString();
                        chunks.Add(new TextChunk(chunkIndex++, content, bufferPageFrom.Value, bufferPageTo));

                        var overlap = content.Length > OverlapChars
                            ? content[^OverlapChars..]
                            : content;

                        buffer.Clear();
                        buffer.Append(overlap);
                        bufferPageFrom = bufferPageTo;
                    }
                }
            }

            if (buffer.Length > 0)
            {
                chunks.Add(new TextChunk(chunkIndex, buffer.ToString(), bufferPageFrom ?? 1, bufferPageTo));
            }

            return chunks;
        }
    }
}

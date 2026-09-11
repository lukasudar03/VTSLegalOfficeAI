namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class AskAnswerResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public List<ChunkSourceDto> Sources { get; set; } = new();
    }

    public class ChunkSourceDto
    {
        public Guid ChunkId { get; set; }
        public int ChunkIndex { get; set; }
        public int PageFrom { get; set; }
        public int PageTo { get; set; }
        public string Excerpt { get; set; } = string.Empty;
    }
}

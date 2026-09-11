namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class ChunkSourceDto
    {
        public Guid ChunkId { get; set; }
        public int ChunkIndex { get; set; }
        public int PageFrom { get; set; }
        public int PageTo { get; set; }
        public string Excerpt { get; set; } = string.Empty;
    }

    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<ChunkSourceDto> Sources { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}

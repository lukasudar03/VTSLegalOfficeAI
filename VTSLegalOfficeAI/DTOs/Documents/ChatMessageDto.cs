namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class ChunkSourceDto
    {
        public Guid ChunkId { get; set; }
        public int ChunkIndex { get; set; }
        public int PageFrom { get; set; }
        public int PageTo { get; set; }
        public string Excerpt { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public bool IsRelatedProvision { get; set; }
    }

    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<ChunkSourceDto> Sources { get; set; } = new();
        public string Confidence { get; set; } = "SREDNJA";
        public string ConfidenceNote { get; set; } = string.Empty;
        public int? DeadlineAmount { get; set; }
        public string? DeadlineUnit { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

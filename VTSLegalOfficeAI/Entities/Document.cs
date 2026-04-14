using System.Text.Json.Serialization;

namespace VTSLegalOfficeAI.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int? TotalPages { get; set; }
        public string Status { get; set; } = "Uploaded";
        public string? ExtractedText { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}
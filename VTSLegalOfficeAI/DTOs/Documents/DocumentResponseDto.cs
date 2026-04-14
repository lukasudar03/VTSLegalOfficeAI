namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class DocumentResponseDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int? TotalPages { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}
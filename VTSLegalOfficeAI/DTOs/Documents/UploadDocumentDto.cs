namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class UploadDocumentDto
    {
        public IFormFile File { get; set; } = null!;
        public string DocumentType { get; set; } = "Pravilnik";
    }
}
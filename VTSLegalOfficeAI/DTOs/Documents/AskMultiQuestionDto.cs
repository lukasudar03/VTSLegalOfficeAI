namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class AskMultiQuestionDto
    {
        public string Question { get; set; } = string.Empty;
        public List<Guid>? DocumentIds { get; set; }
        public bool IsDraftRequest { get; set; }
    }
}

namespace VTSLegalOfficeAI.DTOs.Documents
{
    public class AskQuestionDto
    {
        public string Question { get; set; } = string.Empty;
        public bool IsDraftRequest { get; set; }
    }
}

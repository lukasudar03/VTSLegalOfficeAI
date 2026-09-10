namespace VTSLegalOfficeAI.Services.Models
{
    public record PdfPageText(int PageNumber, string Text);

    public record PdfExtractionResult(string Text, int TotalPages, IReadOnlyList<PdfPageText> Pages);
}

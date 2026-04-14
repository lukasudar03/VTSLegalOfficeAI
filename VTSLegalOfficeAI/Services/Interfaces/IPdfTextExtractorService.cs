namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IPdfTextExtractorService
    {
        (string Text, int TotalPages) ExtractText(string filePath);
    }
}
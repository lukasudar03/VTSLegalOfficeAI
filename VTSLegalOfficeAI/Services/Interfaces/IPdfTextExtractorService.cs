using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Interfaces
{
    public interface IPdfTextExtractorService
    {
        PdfExtractionResult ExtractText(string filePath);
    }
}

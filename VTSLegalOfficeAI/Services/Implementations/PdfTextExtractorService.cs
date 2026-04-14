using System.Text;
using UglyToad.PdfPig;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class PdfTextExtractorService : IPdfTextExtractorService
    {
        public (string Text, int TotalPages) ExtractText(string filePath)
        {
            var sb = new StringBuilder();

            using var document = PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                sb.AppendLine($"--- Page {page.Number} ---");
                sb.AppendLine(page.Text);
                sb.AppendLine();
            }

            return (sb.ToString(), document.NumberOfPages);
        }
    }
}
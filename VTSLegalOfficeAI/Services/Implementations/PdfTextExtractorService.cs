using System.Text;
using UglyToad.PdfPig;
using VTSLegalOfficeAI.Services.Interfaces;
using VTSLegalOfficeAI.Services.Models;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class PdfTextExtractorService : IPdfTextExtractorService
    {
        public PdfExtractionResult ExtractText(string filePath)
        {
            var sb = new StringBuilder();
            var pages = new List<PdfPageText>();

            using var document = PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                pages.Add(new PdfPageText(page.Number, page.Text));

                sb.AppendLine($"--- Page {page.Number} ---");
                sb.AppendLine(page.Text);
                sb.AppendLine();
            }

            return new PdfExtractionResult(sb.ToString(), document.NumberOfPages, pages);
        }
    }
}

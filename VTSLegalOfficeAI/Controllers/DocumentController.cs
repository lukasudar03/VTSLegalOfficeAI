using Microsoft.AspNetCore.Mvc;
using VTSLegalOfficeAI.DTOs.Documents;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentsController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentDto request)
        {
            var document = await _documentService.UploadAsync(request.File);

            return Ok(new
            {
                document.Id,
                document.FileName,
                document.StoredFileName,
                document.FileSizeBytes,
                document.Status,
                document.UploadedAt
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var documents = await _documentService.GetAllAsync();
            return Ok(documents);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var document = await _documentService.GetByIdAsync(id);

            if (document == null)
                return NotFound();

            return Ok(document);
        }
    }
}
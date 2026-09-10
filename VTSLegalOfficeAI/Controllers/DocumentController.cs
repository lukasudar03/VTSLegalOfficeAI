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
        private readonly IQuestionAnsweringService _questionAnsweringService;

        public DocumentsController(IDocumentService documentService, IQuestionAnsweringService questionAnsweringService)
        {
            _documentService = documentService;
            _questionAnsweringService = questionAnsweringService;
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

        [HttpPost("{id:guid}/process")]
        public async Task<IActionResult> Process(Guid id)
        {
            await _documentService.ProcessDocumentAsync(id);
            return Ok(new { Message = "Document processed successfully." });
        }

        [HttpPost("{id:guid}/ask")]
        public async Task<IActionResult> Ask(Guid id, [FromBody] AskQuestionDto request)
        {
            var result = await _questionAnsweringService.AskAsync(id, request.Question);

            return Ok(new AskAnswerResponseDto
            {
                Answer = result.Answer,
                Sources = result.Sources.Select(s => new ChunkSourceDto
                {
                    ChunkId = s.ChunkId,
                    ChunkIndex = s.ChunkIndex,
                    PageFrom = s.PageFrom,
                    PageTo = s.PageTo,
                    Excerpt = s.Content.Length > 300 ? s.Content[..300] + "…" : s.Content
                }).ToList()
            });
        }
    }
}

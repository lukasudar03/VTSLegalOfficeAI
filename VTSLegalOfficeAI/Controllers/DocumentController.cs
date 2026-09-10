using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VTSLegalOfficeAI.DTOs.Documents;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IQuestionAnsweringService _questionAnsweringService;

        public DocumentsController(IDocumentService documentService, IQuestionAnsweringService questionAnsweringService)
        {
            _documentService = documentService;
            _questionAnsweringService = questionAnsweringService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentDto request)
        {
            var document = await _documentService.UploadAsync(request.File, CurrentUserId);

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
            var documents = await _documentService.GetAllAsync(CurrentUserId);
            return Ok(documents);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var document = await _documentService.GetByIdAsync(id, CurrentUserId);

            if (document == null)
                return NotFound();

            return Ok(document);
        }

        [HttpPost("{id:guid}/process")]
        public async Task<IActionResult> Process(Guid id)
        {
            await _documentService.ProcessDocumentAsync(id, CurrentUserId);
            return Ok(new { Message = "Document processed successfully." });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _documentService.DeleteDocumentAsync(id, CurrentUserId);
            return NoContent();
        }

        [HttpGet("{id:guid}/file")]
        public async Task<IActionResult> GetFile(Guid id)
        {
            var document = await _documentService.GetByIdAsync(id, CurrentUserId);
            if (document == null)
                return NotFound();

            if (!System.IO.File.Exists(document.FilePath))
                return NotFound();

            var stream = System.IO.File.OpenRead(document.FilePath);
            return File(stream, "application/pdf");
        }

        [HttpPost("{id:guid}/ask")]
        public async Task<IActionResult> Ask(Guid id, [FromBody] AskQuestionDto request)
        {
            var result = await _questionAnsweringService.AskAsync(id, CurrentUserId, request.Question);

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

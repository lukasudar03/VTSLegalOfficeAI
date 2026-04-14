using System.Text.Json.Serialization;
using Pgvector;

namespace VTSLegalOfficeAI.Entities
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }

        [JsonIgnore]
        public Document Document { get; set; } = null!;

        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public int PageFrom { get; set; }
        public int PageTo { get; set; }

        [JsonIgnore]
        public Vector Embedding { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
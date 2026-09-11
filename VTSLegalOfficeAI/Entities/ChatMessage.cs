using System.Text.Json.Serialization;

namespace VTSLegalOfficeAI.Entities
{
    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string SourcesJson { get; set; } = "[]";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Document Document { get; set; } = null!;
    }
}

using System.Text.Json.Serialization;

namespace VTSLegalOfficeAI.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}

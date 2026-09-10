namespace VTSLegalOfficeAI.Options
{
    public class OllamaOptions
    {
        public const string SectionName = "Ollama";

        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string EmbeddingModel { get; set; } = "nomic-embed-text";
        public string ChatModel { get; set; } = "qwen3:8b";
    }
}

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using VTSLegalOfficeAI.Options;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OllamaOptions _options;

        public OllamaEmbeddingService(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task<List<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (texts.Count == 0)
                return new List<float[]>();

            var client = _httpClientFactory.CreateClient("Ollama");

            var requestBody = new
            {
                model = _options.EmbeddingModel,
                input = texts
            };

            var response = await client.PostAsJsonAsync("/api/embed", requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Ollama /api/embed returned {(int)response.StatusCode}: {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: cancellationToken);

            if (result?.Embeddings == null || result.Embeddings.Count != texts.Count)
                throw new InvalidOperationException("Ollama did not return the expected number of embeddings.");

            return result.Embeddings;
        }

        private class OllamaEmbedResponse
        {
            [JsonPropertyName("embeddings")]
            public List<float[]>? Embeddings { get; set; }
        }
    }
}

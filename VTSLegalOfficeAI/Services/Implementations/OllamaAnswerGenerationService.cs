using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using VTSLegalOfficeAI.Options;
using VTSLegalOfficeAI.Services.Interfaces;

namespace VTSLegalOfficeAI.Services.Implementations
{
    public class OllamaAnswerGenerationService : IAnswerGenerationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OllamaOptions _options;

        public OllamaAnswerGenerationService(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task<string> GenerateAnswerAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("Ollama");

            var requestBody = new
            {
                model = _options.ChatModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = false,
                think = false
            };

            var response = await client.PostAsJsonAsync("/api/chat", requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Ollama /api/chat returned {(int)response.StatusCode}: {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);

            var answer = result?.Message?.Content?.Trim();
            if (string.IsNullOrEmpty(answer))
                throw new InvalidOperationException("Ollama did not return an answer.");

            return answer;
        }

        private class OllamaChatResponse
        {
            [JsonPropertyName("message")]
            public OllamaChatMessage? Message { get; set; }
        }

        private class OllamaChatMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}

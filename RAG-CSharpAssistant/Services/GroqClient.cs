using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace RAG_CSharpAssistant.Services;

/// <summary>
/// OpenAI-compatible Groq Chat Completions client.
/// Endpoint: <c>https://api.groq.com/openai/v1/chat/completions</c>.
/// Mirrors the Flutter pipeline's behavior — same model, parameters, headers, and SSE parsing.
/// </summary>
public sealed class GroqClient : IGroqClient
{
    private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model = "llama-3.1-8b-instant";

    private readonly HttpClient _http;

    public GroqClient(HttpClient http)
    {
        _http = http;
        // Streaming requests can run for a while; rely on CancellationToken instead of a wall-clock timeout.
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
        => !string.IsNullOrWhiteSpace(await AppSecrets.GetGroqApiKeyAsync().ConfigureAwait(false));

    public async Task<bool> IsCSharpScopeAsync(string userQuery, CancellationToken ct = default)
    {
        var apiKey = await AppSecrets.GetGroqApiKeyAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(AppSecrets.MissingKeyMessage);

        var trimmed = userQuery?.Trim() ?? string.Empty;

        var body = new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = PromptBuilder.ScopeSystemPrompt },
                new { role = "user",   content = trimmed },
            },
            temperature = 0,
            max_tokens = 5,
            stream = false,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Groq scope check failed ({(int)resp.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
                         .GetProperty("choices")[0]
                         .GetProperty("message")
                         .GetProperty("content")
                         .GetString() ?? string.Empty;

        // Flutter parsing: first whitespace-delimited word, uppercased; accept "YES" or "Y".
        var firstWord = content.Trim()
            .Split(new[] { ' ', '\t', '\r', '\n' }, 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.ToUpperInvariant() ?? string.Empty;

        return firstWord is "YES" or "Y";
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var apiKey = await AppSecrets.GetGroqApiKeyAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(AppSecrets.MissingKeyMessage);

        var body = new
        {
            model = Model,
            // IMPORTANT (Flutter parity): a single user message carrying the whole prompt — no system message.
            messages = new object[]
            {
                new { role = "user", content = prompt },
            },
            temperature = 0.1,
            stream = true,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                                    .ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException($"Groq stream failed ({(int)resp.StatusCode}): {err}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) yield break; // upstream closed the connection
            if (line.Length == 0 || !line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var data = line.Substring(6).Trim();
            if (data == "[DONE]")
                yield break;

            string? chunk = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0) continue;

                var delta = choices[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    chunk = c.GetString();
            }
            catch (JsonException)
            {
                // Ignore malformed SSE frames — Groq occasionally emits keep-alive comments.
                continue;
            }

            if (!string.IsNullOrEmpty(chunk))
                yield return chunk;
        }
    }
}

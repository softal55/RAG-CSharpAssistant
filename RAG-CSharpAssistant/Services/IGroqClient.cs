namespace RAG_CSharpAssistant.Services;

public interface IGroqClient
{
    /// <summary>True once a Groq API key has been resolved.</summary>
    Task<bool> IsConfiguredAsync(CancellationToken ct = default);

    /// <summary>
    /// Stage 1: strict YES/NO scope check. Non-streaming, temperature 0, max_tokens 5.
    /// Returns true only when the model emits "YES" or "Y" as the first whitespace-delimited token.
    /// </summary>
    Task<bool> IsCSharpScopeAsync(string userQuery, CancellationToken ct = default);

    /// <summary>
    /// Stage 4: streaming chat completion with a single user message containing the entire prompt.
    /// Yields each <c>delta.content</c> chunk as it arrives over SSE.
    /// </summary>
    IAsyncEnumerable<string> GenerateStreamAsync(string prompt, CancellationToken ct = default);
}

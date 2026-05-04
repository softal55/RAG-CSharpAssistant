namespace RAG_CSharpAssistant.Services;

public interface IRagSearchService
{
    /// <summary>True once the bundled knowledge base has been provisioned successfully.</summary>
    bool IsReady { get; }

    /// <summary>Last initialization error (e.g. missing database).</summary>
    string? InitializationError { get; }

    /// <summary>Ensures the knowledge base is provisioned (copied out of the app package).</summary>
    Task EnsureInitializedAsync(CancellationToken ct = default);

    /// <summary>
    /// Stage 2: Flutter-parity FTS5 retrieval.
    /// Strips quotes, tokenises on whitespace, runs an AND prefix query (top-3),
    /// and falls back to an OR prefix query if the AND query returns no rows.
    /// Returns the <c>chunk</c> column only, in BM25-ranked order.
    /// </summary>
    Task<IReadOnlyList<string>> SearchAsync(string userQuery, CancellationToken ct = default);
}

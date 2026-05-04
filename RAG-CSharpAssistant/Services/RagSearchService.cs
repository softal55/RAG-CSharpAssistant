using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace RAG_CSharpAssistant.Services;

/// <summary>
/// SQLite/FTS5 retrieval over the knowledge base built by <c>DataPipeline/build_db.py</c>.
/// The retrieval algorithm matches the Flutter app character-for-character:
///   1. Replace every <c>'</c> and <c>"</c> with a space, trim. (FTS5 syntax errors otherwise.)
///   2. Tokenise on whitespace.
///   3. Build "tok1* tok2* tok3*" (AND, prefix-match) and "tok1* OR tok2* OR tok3*" (OR fallback).
///   4. Run AND first (LIMIT 3). If empty, run OR (LIMIT 3).
///   5. Order by metadata.is_accepted DESC, bm25(qa_index, 1.0, 0.3, 0.8) ASC.
///   6. Return the chunk column only.
/// </summary>
public sealed class RagSearchService : IRagSearchService
{
    private const string DbAssetName = "csharp_knowledge.db";

    private const string FtsSql = """
        SELECT qa_index.chunk,
               metadata.score,
               metadata.is_accepted,
               bm25(qa_index, 1.0, 0.3, 0.8) AS rank_score
        FROM qa_index
        JOIN metadata ON qa_index.rowid = metadata.rowid
        WHERE qa_index MATCH $q
        ORDER BY metadata.is_accepted DESC,
                 rank_score ASC
        LIMIT 3;
    """;

    private static readonly Regex QuoteScrubber = new("['\"]", RegexOptions.Compiled);

    /// <summary>
    /// Splits the query on anything that isn't a valid FTS5 tokenchar — i.e. anything
    /// outside of <c>[A-Za-z0-9#&lt;&gt;._]</c>. Whitespace is included by virtue of
    /// not being a tokenchar. This turns noisy code-like text such as
    /// <c>"(Console.WindowWidth - gameWindowWidth)/2;"</c> into clean tokens
    /// (<c>Console.WindowWidth</c>, <c>gameWindowWidth</c>, <c>2</c>) instead of
    /// gluing punctuation onto the words and feeding it to FTS5.
    /// </summary>
    private static readonly Regex TokenSplitter = new(@"[^A-Za-z0-9#<>._]+", RegexOptions.Compiled);

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private string? _dbPath;
    private SqliteConnection? _connection;
    private bool _ready;

    public bool IsReady => _ready;
    public string? InitializationError { get; private set; }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_ready) return;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ready) return;

            var targetPath = Path.Combine(FileSystem.AppDataDirectory, DbAssetName);

            if (!File.Exists(targetPath))
            {
                try
                {
                    using var src = await FileSystem.OpenAppPackageFileAsync(DbAssetName)
                                                    .ConfigureAwait(false);
                    using var dst = File.Create(targetPath);
                    await src.CopyToAsync(dst, ct).ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    InitializationError =
                        $"Knowledge base '{DbAssetName}' is not bundled. " +
                        "Run DataPipeline/build_db.py to generate it, then rebuild the app.";
                    return;
                }
            }

            _dbPath = targetPath;
            _connection = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            await _connection.OpenAsync(ct).ConfigureAwait(false);

            _ready = true;
            InitializationError = null;
        }
        catch (Exception ex)
        {
            InitializationError = $"Failed to initialize knowledge base: {ex.Message}";
            _connection?.Dispose();
            _connection = null;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> SearchAsync(string userQuery, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);
        if (!_ready || _connection is null)
            return Array.Empty<string>();

        // 1) Strip quotes, trim — Flutter parity (FTS5 syntax error on raw ' or " inside MATCH).
        var clean = QuoteScrubber.Replace(userQuery ?? string.Empty, " ").Trim();
        if (string.IsNullOrEmpty(clean))
            return Array.Empty<string>();

        // 2) Tokenise on whitespace AND on non-tokenchars so noisy code-like input
        //    (parens, slashes, semicolons, hyphens …) doesn't poison the MATCH expression.
        //    Also drop ultra-short tokens (length <= 1): a single-char prefix like "s"*
        //    matches almost the entire corpus and poisons the OR fallback, dragging in
        //    completely unrelated rows for short queries such as "what's c#?".
        var tokens = TokenSplitter
            .Split(clean)
            .Where(t => t.Length >= 2)
            .ToArray();

        if (tokens.Length == 0)
            return Array.Empty<string>();

        // 3) Build prefix-matched AND and OR expressions. Each token MUST be wrapped in
        //    double quotes — the FTS5 query parser otherwise rejects bare tokens that
        //    contain tokenchars like '#', '<', '>', '.' (e.g. `C#*` → "syntax error near #").
        //    The custom tokenizer in the DB still treats them as part of the indexed word,
        //    so `"C#"*` correctly prefix-matches the indexed token `c#`.
        var andQuery = string.Join(" ",     tokens.Select(t => "\"" + t + "\"*"));
        var orQuery  = string.Join(" OR ",  tokens.Select(t => "\"" + t + "\"*"));

        Debug.WriteLine($"[RAG] tokens={tokens.Length} AND='{Truncate(andQuery, 200)}'");

        // 4) AND first; OR is the fallback if AND returns no rows.
        var hits = await RunFtsAsync(andQuery, ct).ConfigureAwait(false);
        Debug.WriteLine($"[RAG] AND hits={hits.Count}");
        if (hits.Count == 0)
        {
            hits = await RunFtsAsync(orQuery, ct).ConfigureAwait(false);
            Debug.WriteLine($"[RAG] OR  hits={hits.Count}");
        }

        return hits;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

    private async Task<List<string>> RunFtsAsync(string match, CancellationToken ct)
    {
        var list = new List<string>(3);
        if (_connection is null) return list;

        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = FtsSql;
            cmd.Parameters.AddWithValue("$q", match);

            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
                list.Add(r.GetString(0));
        }
        catch (SqliteException)
        {
            // Match Flutter behavior: any FTS5 syntax error → silently treat as "no rows".
        }
        return list;
    }
}

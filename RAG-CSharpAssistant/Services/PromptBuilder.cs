namespace RAG_CSharpAssistant.Services;

/// <summary>
/// Verbatim Flutter prompts. Whitespace, line breaks, and headings must match exactly —
/// llama-3.1-8b-instant is sensitive to small differences.
/// </summary>
public static class PromptBuilder
{
    /// <summary>System prompt for the YES/NO scope classifier (Stage 1).</summary>
    public const string ScopeSystemPrompt =
        "You are a strict classifier. Reply with exactly YES or NO, nothing else.\n" +
        "YES = the user message is primarily about C# programming, the .NET runtime or BCL, ASP.NET Core in C#, or tooling used to write or debug C# code.\n" +
        "NO = general knowledge, other programming languages (unless comparing a tiny snippet to C#), math, homework in other subjects, chitchat, recipes, politics, personal advice, or anything not centered on C# / .NET development.";

    /// <summary>Block message returned when the scope check rejects the question (Stage 1).</summary>
    public const string OutOfScopeMessage =
        "I only answer questions related to C# / .NET. " +
        "If your question is in the local knowledge base I'll answer from it; " +
        "otherwise I can still generate an answer from general C# / .NET knowledge.";

    /// <summary>Disclaimer streamed to the chat before a grounded answer (Stage 3a).</summary>
    public const string GroundedDisclaimer =
        "Found relevant entries in your local C# knowledge base. The answer below is grounded in those snippets.";

    /// <summary>Disclaimer streamed to the chat before an ungrounded answer (Stage 3b).</summary>
    public const string UngroundedDisclaimer =
        "No entries matched your local C# database. What follows is general C# / .NET knowledge, not from your offline snippets.";

    /// <summary>Grounded RAG prompt — sent as a single user message (Stage 3a).</summary>
    /// <remarks>
    /// The question has already passed the Stage-1 C# scope classifier, so the model must
    /// NEVER reply with the off-scope refusal here. The CONTEXT is the primary source of
    /// truth, but if it doesn't directly cover the question the model is allowed to
    /// supplement with general C# / .NET knowledge — this prevents irrelevant FTS5 hits
    /// from forcing a refusal on legitimate C# questions like "what's c#?".
    /// </remarks>
    public static string BuildGrounded(string userQuery, IReadOnlyList<string> chunks)
    {
        var joined = string.Join("\n\n---\n\n", chunks);
        return
            "You are a C# / .NET assistant. The QUESTION has already been confirmed to be about C# / .NET, and the user has already seen a UI line saying that relevant entries were found in the local knowledge base — do NOT repeat that disclaimer, and do NOT refuse the question.\n" +
            "Use the CONTEXT below (local Stack Overflow–style C# snippets) as your primary source of truth.\n" +
            "Rules:\n" +
            "1. If the CONTEXT directly addresses the QUESTION, answer using it. Do not invent APIs, syntax, or behavior beyond what the CONTEXT supports.\n" +
            "2. If the CONTEXT only partially addresses the QUESTION, use what is relevant and supplement with your general C# / .NET knowledge. Mark any clearly supplemental claim with a brief note like \"(general knowledge)\".\n" +
            "3. If the CONTEXT is not actually relevant to the QUESTION, briefly note that the local snippets did not cover it and answer from your general C# / .NET knowledge.\n" +
            "4. Stay focused on C# / .NET. Never reply with the off-scope refusal — it has already been handled upstream.\n" +
            "CONTEXT:\n" +
            joined + "\n" +
            "QUESTION:\n" +
            userQuery;
    }

    /// <summary>Ungrounded fallback prompt — sent as a single user message (Stage 3b).</summary>
    public static string BuildUngrounded(string userQuery)
    {
        return
            "You are a C# and .NET programming assistant.\n" +
            "Nothing in the user's local knowledge base matched their question, but their question was already confirmed to be about C# or .NET development.\n" +
            "Answer using your general knowledge of C# and .NET only (language, BCL, runtime, common libraries, idioms). Do not pivot to unrelated topics or other ecosystems unless the question explicitly asks for a brief comparison.\n" +
            "The user interface already stated that this answer is not from the local database; do not repeat that disclaimer—go straight into the technical answer.\n" +
            "If you still cannot answer as a C#/.NET developer question, say so briefly.\n" +
            "QUESTION:\n" +
            userQuery;
    }
}

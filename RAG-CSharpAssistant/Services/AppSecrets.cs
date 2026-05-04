namespace RAG_CSharpAssistant.Services;

/// <summary>
/// Resolves the Groq API key from (in order):
///  1. <c>GROQ_API_KEY</c> environment variable (handy on dev machines).
///  2. <c>groq.key</c> bundled at <c>Resources\Raw\groq.key</c> (cross-platform — Android/iOS/Windows).
///  3. <c>Preferences</c> entry "GroqApiKey" (settable from a future settings page).
/// </summary>
public static class AppSecrets
{
    private static string? _cachedGroqKey;

    public static async Task<string?> GetGroqApiKeyAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedGroqKey))
            return _cachedGroqKey;

        var env = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (!string.IsNullOrWhiteSpace(env))
            return _cachedGroqKey = env.Trim();

        try
        {
            using var s = await FileSystem.OpenAppPackageFileAsync("groq.key").ConfigureAwait(false);
            using var r = new StreamReader(s);
            var fileKey = (await r.ReadToEndAsync().ConfigureAwait(false)).Trim();
            if (!string.IsNullOrWhiteSpace(fileKey))
                return _cachedGroqKey = fileKey;
        }
        catch (FileNotFoundException) { /* no bundled key — fall through */ }

        var pref = Preferences.Default.Get("GroqApiKey", string.Empty);
        if (!string.IsNullOrWhiteSpace(pref))
            return _cachedGroqKey = pref.Trim();

        return null;
    }

    public const string MissingKeyMessage =
        "Groq API key is not configured.\n" +
        "Add your key in one of these places, then rebuild/restart the app:\n" +
        "  1) Set the GROQ_API_KEY environment variable, or\n" +
        "  2) Drop a one-line file at Resources/Raw/groq.key (rebuild required), or\n" +
        "  3) Preferences.Default.Set(\"GroqApiKey\", \"...\")\n";
}

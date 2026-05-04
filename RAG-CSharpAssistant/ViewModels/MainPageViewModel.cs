using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RAG_CSharpAssistant.Models;
using RAG_CSharpAssistant.Services;

namespace RAG_CSharpAssistant.ViewModels;

public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private readonly IRagSearchService _search;
    private readonly IGroqClient _groq;

    private string _inputText = string.Empty;
    private bool _isGenerating;
    private double _maxBubbleWidth = 320;

    public MainPageViewModel(IRagSearchService search, IGroqClient groq)
    {
        _search = search;
        _groq = groq;

        SendCommand = new Command(
            execute: async () => await SendAsync(),
            canExecute: () => !IsGenerating && !string.IsNullOrWhiteSpace(InputText));

        // Warm-up: copy the bundled DB out of the app package so the first query feels instant.
        // Run on the threadpool — on Android the asset stream may do synchronous Java I/O,
        // which would otherwise execute on the UI thread (NetworkOnMainThreadException).
        _ = Task.Run(() => _search.EnsureInitializedAsync());
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText == value) return;
            _inputText = value;
            OnPropertyChanged();
            ((Command)SendCommand).ChangeCanExecute();
        }
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (_isGenerating == value) return;
            _isGenerating = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsInputEnabled));
            ((Command)SendCommand).ChangeCanExecute();
        }
    }

    public bool IsInputEnabled => !IsGenerating;

    /// <summary>Updated by the page on size changes; caps each bubble at 80% of page width.</summary>
    public double MaxBubbleWidth
    {
        get => _maxBubbleWidth;
        set
        {
            if (Math.Abs(_maxBubbleWidth - value) < 0.5) return;
            _maxBubbleWidth = value;
            OnPropertyChanged();
        }
    }

    public ICommand SendCommand { get; }

    /// <summary>Raised whenever the active bot message receives a new streamed chunk.</summary>
    public event Action<ChatMessage>? MessageStreamed;

    private async Task SendAsync()
    {
        if (IsGenerating) return;

        var prompt = InputText?.Trim() ?? string.Empty;
        if (prompt.Length == 0) return;

        Messages.Add(new ChatMessage(prompt, isUser: true));
        InputText = string.Empty;

        var bot = new ChatMessage(string.Empty, isUser: false);
        Messages.Add(bot);

        IsGenerating = true;

        // Drive the entire pipeline (HTTP scope check, FTS5, SSE streaming) on the
        // threadpool. The Android HTTP handler (AndroidMessageHandler / OkHttp) does
        // synchronous Java I/O internally; if that runs on the UI thread Android
        // throws NetworkOnMainThreadException. Each streamed token is marshalled
        // back to the UI thread before mutating the bound bot bubble.
        Exception? failure = null;
        await Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in StreamPipelineAsync(prompt).ConfigureAwait(false))
                {
                    var c = chunk;
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        bot.Text += c;
                        MessageStreamed?.Invoke(bot);
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }).ConfigureAwait(true);

        if (failure is not null)
            bot.Text = $"Error generating response.\n{failure.Message}";
        else if (bot.Text.Length == 0)
            bot.Text = "Error generating response.";

        IsGenerating = false;
    }

    /// <summary>
    /// Flutter-parity 4-stage pipeline:
    ///   [1] Groq YES/NO scope check  → on NO yield the block message and stop.
    ///   [2] FTS5 retrieval           → AND prefix query, fallback to OR; top-3 chunks.
    ///   [3] Build the grounded or ungrounded prompt (verbatim).
    ///   [4] Stream the model output via SSE into the bot bubble token-by-token.
    /// </summary>
    private async IAsyncEnumerable<string> StreamPipelineAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // ConfigureAwait(false) throughout: this iterator is consumed on the threadpool
        // (see SendAsync). We must NOT bounce back to the UI thread between awaits, or
        // the next network read could happen on the UI thread on Android.
        if (!await _groq.IsConfiguredAsync(ct).ConfigureAwait(false))
        {
            yield return AppSecrets.MissingKeyMessage;
            yield break;
        }

        // Stage 1 — scope check.
        var inScope = await _groq.IsCSharpScopeAsync(prompt, ct).ConfigureAwait(false);
        if (!inScope)
        {
            yield return PromptBuilder.OutOfScopeMessage;
            yield break;
        }

        // Stage 2 — FTS5 retrieval.
        await _search.EnsureInitializedAsync(ct).ConfigureAwait(false);
        if (!_search.IsReady)
        {
            yield return _search.InitializationError ?? "Knowledge base unavailable.";
            yield break;
        }

        var chunks = await _search.SearchAsync(prompt, ct).ConfigureAwait(false);

        // Stage 3 — prompt construction.
        // The disclaimer line is shown to the user only — it is NOT part of the LLM prompt,
        // so the model itself doesn't repeat it.
        string llmPrompt;
        if (chunks.Count == 0)
        {
            yield return PromptBuilder.UngroundedDisclaimer + "\n\n";
            llmPrompt = PromptBuilder.BuildUngrounded(prompt);
        }
        else
        {
            yield return PromptBuilder.GroundedDisclaimer + "\n\n";
            llmPrompt = PromptBuilder.BuildGrounded(prompt, chunks);
        }

        // Stage 4 — streaming generation (single user message, temperature 0.1).
        await foreach (var token in _groq.GenerateStreamAsync(llmPrompt, ct).ConfigureAwait(false))
            yield return token;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

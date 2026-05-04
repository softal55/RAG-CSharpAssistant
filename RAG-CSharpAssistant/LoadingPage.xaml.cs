using Microsoft.Extensions.DependencyInjection;
using RAG_CSharpAssistant.Services;

namespace RAG_CSharpAssistant;

/// <summary>
/// In-app splash. Renders splash_full.png full-bleed (see LoadingPage.xaml).
/// No element-level animation, no layout reconstruction — what the user sees
/// is the design mockup, pixel-identical.
///
/// The OnAppearing override holds the splash for ~1.22 s (per spec) and
/// silently performs two pieces of backstage work during that hold so the
/// transition into AppShell is snappy:
///   1) FTS5 knowledge-base warmup on the threadpool.
///   2) MainPage XAML inflation on the UI thread (MainPage is a Singleton, so
///      the same instance is reused when AppShell.ContentTemplate resolves it).
/// Both are invisible and don't affect the visual splash; they just prevent
/// the chat from costing inflation + cold-DB time on first show.
/// </summary>
public partial class LoadingPage : ContentPage
{
    private readonly IRagSearchService _search;
    private readonly IServiceProvider _services;

    public LoadingPage(IRagSearchService search, IServiceProvider services)
    {
        InitializeComponent();
        _search = search;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Silent backstage work — runs in parallel with the visible hold.
        var warmup = Task.Run(() => _search.EnsureInitializedAsync());
        _ = _services.GetRequiredService<MainPage>();

        // Per spec: 20 ms to let the first frame paint, then a 1200 ms hold so
        // the splash reads as deliberate rather than as a flash.
        await Task.Delay(20);
        await Task.Delay(1200);

        // Make sure the DB warmup has finished. Almost always already done by
        // now (1.22 s is plenty); guarantees the chat is queryable on first
        // show on a slow first launch.
        await warmup;

        // Swap to the chat. Application.MainPage is deprecated in MAUI 10
        // (CS0618); Window.Page is the supported replacement and has the same
        // effect.
        if (Window is { } window)
            window.Page = new AppShell();
    }
}

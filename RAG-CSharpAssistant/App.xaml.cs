using Microsoft.Extensions.DependencyInjection;

namespace RAG_CSharpAssistant
{
    public partial class App : Application
    {
        private readonly IServiceProvider _services;

        public App(IServiceProvider services)
        {
            InitializeComponent();

            _services = services;

            // The chat experience is dark-only. The splash screen sets its own
            // light beige background explicitly, so it is unaffected by this.
            UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Start on the in-app splash. LoadingPage.OnAppearing warms up the
            // FTS5 knowledge base, then swaps Window.Page to AppShell.
            var loading = _services.GetRequiredService<LoadingPage>();
            return new Window(loading);
        }
    }
}
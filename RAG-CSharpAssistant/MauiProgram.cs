using Microsoft.Extensions.Logging;
using RAG_CSharpAssistant.Services;
using RAG_CSharpAssistant.ViewModels;

namespace RAG_CSharpAssistant
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<HttpClient>(_ => new HttpClient());
            builder.Services.AddSingleton<IRagSearchService, RagSearchService>();
            builder.Services.AddSingleton<IGroqClient, GroqClient>();
            // MainPage and its ViewModel are singletons so we can pre-build
            // them during the splash; the same cached instance is then handed
            // back to AppShell.ContentTemplate when the swap happens, skipping
            // a second XAML inflation. Chat history persists naturally too.
            builder.Services.AddSingleton<MainPageViewModel>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<LoadingPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

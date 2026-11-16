using MauiApp1.Services;
using Microsoft.Extensions.Logging;

namespace MauiApp1
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
                });

            builder.Services.AddMauiBlazorWebView();

            // Một HttpClient dùng chung cho toàn app (gọi MorningBrew.Api).
            builder.Services.AddSingleton(_ => new HttpClient
            {
                BaseAddress = new Uri(ApiSettings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(20)
            });
            builder.Services.AddSingleton<ApiClient>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<CrmService>();
            builder.Services.AddSingleton<ShopService>();
            builder.Services.AddSingleton<ToastService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

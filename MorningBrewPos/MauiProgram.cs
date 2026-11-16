using MorningBrew.Pos.Services;
using Microsoft.Extensions.Logging;

namespace MorningBrew.Pos;

// [BUG-02 · giai đoạn 2] Khởi tạo app POS: dùng lại ApiClient/ApiJson/ToastService của app khách.
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(ApiSettings.BaseUrl), Timeout = TimeSpan.FromSeconds(20) });
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<PosAuth>();
        builder.Services.AddSingleton<PosService>();
        builder.Services.AddSingleton<ToastService>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}

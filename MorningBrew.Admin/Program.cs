using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MorningBrew.Admin.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<CrmApi>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7132/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
Portals.Load(builder.Configuration); // [SEP-01] Admin server (5190) và Quản lý CRM (5191)
// [SEP-02] Mỗi giao diện một cơ chế đăng nhập (cookie) riêng; "PortalByPort" chọn cookie theo cổng của request,
// nên đăng nhập ở cổng này không tự đăng nhập cổng kia, đăng xuất cũng độc lập.
builder.Services.AddAuthentication(Portals.Selector)
    .AddPolicyScheme(Portals.Selector, "Theo cổng", options => options.ForwardDefaultSelector = context => (Portals.Of(context) ?? Portals.Manager).Scheme)
    .AddCookie(Portals.Admin.Scheme, options => ConfigureCookie(options, Portals.Admin.CookieName))
    .AddCookie(Portals.Manager.Scheme, options => ConfigureCookie(options, Portals.Manager.CookieName));
void ConfigureCookie(CookieAuthenticationOptions options, string cookieName) // [SEP-02] cấu hình cũ, chỉ đổi tên cookie theo giao diện
{
    options.Cookie.Name = cookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnValidatePrincipal = async context =>
    {
        try
        {
            var api = context.HttpContext.RequestServices.GetRequiredService<CrmApi>();
            var token=context.Properties.GetTokenValue("access_token");
            if(string.IsNullOrWhiteSpace(token)){context.RejectPrincipal();return;}
            var me = await api.Me(token);
            if (me.Role != context.Principal?.FindFirstValue(ClaimTypes.Role)) context.RejectPrincipal();
        }
        catch (CrmApiException e) when (e.Status == 401) { context.RejectPrincipal(); }
        // Keep the protected cookie during a temporary outage; API calls still enforce access.
        catch (CrmApiException e) when (e.Status == 503) { }
    };
}
builder.Services.AddAuthorization(options =>
{
    // Hai giao diện tách biệt: /Admin (quản trị hệ thống, chỉ chủ cửa hàng) và /Manager (nghiệp vụ CRM).
    options.AddPolicy("Admin", policy => policy.RequireRole("Owner"));
    options.AddPolicy("Management", policy => policy.RequireRole("Owner","BranchManager"));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AllowAnonymousToPage("/WrongPortal"); // [SEP-03]
    options.Conventions.AuthorizeFolder("/Admin","Admin");
    options.Conventions.AuthorizeFolder("/Manager","Management");
});
var app = builder.Build();
app.UseExceptionHandler("/Error");
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
// [SEP-03] Chặn theo cổng: cổng Admin server chỉ mở /Admin/*, cổng Quản lý CRM chỉ mở /Manager/*.
// Vào nhầm khu thì chuyển sang trang WrongPortal (có link tới đúng địa chỉ); cổng lạ trả 404.
app.Use(async (context, next) =>
{
    var portal = Portals.Of(context);
    if (portal is null) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
    var other = Portals.Other(portal);
    if (context.Request.Path.StartsWithSegments(other.Prefix))
    {
        context.Response.Redirect("/WrongPortal?path=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        return;
    }
    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();

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
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "MorningBrew.Admin.Session";
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
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Management", policy => policy.RequireRole("Owner","AreaManager","BranchManager"));
    options.AddPolicy("Customer", policy => policy.RequireRole("Customer"));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizePage("/Index","Management");
    options.Conventions.AuthorizePage("/Workspace","Management");
    options.Conventions.AuthorizePage("/Portal","Customer");
    options.Conventions.AuthorizePage("/Answer","Customer");
});
var app = builder.Build();
app.UseExceptionHandler("/Error");
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();

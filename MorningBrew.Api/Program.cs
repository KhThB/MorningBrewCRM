using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using MorningBrew.Api.Infrastructure;
using MorningBrew.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SqlStore>();
builder.Services.AddAuthentication("Session")
    .AddScheme<AuthenticationSchemeOptions, SessionAuthentication>("Session", null);
builder.Services.AddAuthorization();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<PageReader>();
builder.Services.AddSingleton<DatabaseBackup>(); // [UI2-W3a] sao lưu / phục hồi trên web
builder.Services.AddHostedService<AutoBackupService>(); // [UI2-W3a] sao lưu tự động hằng ngày
builder.Services.AddHostedService<UnpaidOrderExpiry>(); // [BUG-02] tự hủy đơn trả trước quá hạn thanh toán mỗi phút
builder.Services.AddControllers(options => options.Filters.Add<ApiStatusFilter>())
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ApiContract.Validation);
builder.Services.ConfigureHttpJsonOptions(options => ApiJson.Configure(options.SerializerOptions));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // [SWAGGER] Hai controller có class lồng cùng tên (AdminController.AdvanceInput, PosController.AdvanceInput)
    // → đặt schemaId kèm tên lớp chứa để Swagger không báo trùng.
    options.CustomSchemaIds(SwaggerSchemaId);
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MorningBrew CRM API", Version = "v1",
        Description = "JSON snake_case; query pageSize/pageToken; UTC timestamps; opaque bearer sessions. Prices are shared across the chain." });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "Opaque session token",
        Description = "Đăng nhập ở POST /v1/auth/login rồi dán token vào đây."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    options.OperationFilter<ApiDocumentation>();
});
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
        .WithExposedHeaders("ETag", "Location", "Retry-After");
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = (context, cancellation) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseMiddleware<ApiErrors>();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});
app.UseStatusCodePages(async status =>
    await status.HttpContext.Response.WriteAsJsonAsync(ApiContract.Error(status.HttpContext.Response.StatusCode)));
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
else app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles(); // [UI2-IMG] phục vụ ảnh sản phẩm tải lên: /uploads/products/...
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (SqlStore db) =>
{
    await db.Query("SELECT TOP(1) RoleID FROM dbo.Roles");
    return Results.Ok(new { status = "ready", database = "SqlServer" });
});
app.Run();

// [SWAGGER] Tên schema: lớp lồng → "Lớp chứa.Tên", generic → "ApiPageOfOrder".
static string SwaggerSchemaId(Type t) =>
    t.IsGenericType ? t.Name.Split('`')[0] + "Of" + string.Join("And", t.GetGenericArguments().Select(SwaggerSchemaId))
    : t.IsNested ? SwaggerSchemaId(t.DeclaringType!) + "." + t.Name
    : t.Name;

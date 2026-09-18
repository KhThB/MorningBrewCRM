using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
// using OpenApi Models removed
using MorningBrew.Api.Common;
using MorningBrew.Api.Data;
using MorningBrew.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Bind JwtSettings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();

// 1. Thêm Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication setup
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSettings.SecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 2. Đăng ký ApplicationDbContext và cấu hình các quan hệ bằng Fluent API
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("MorningBrewDb"));
// Các service hiện tại nhận DbContext cơ sở; ánh xạ chúng vào context đã cấu hình.
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// 3. Đăng ký các Services B2
builder.Services.AddScoped<MorningBrew.Api.Services.Interfaces.ICustomerService, MorningBrew.Api.Services.Implementations.CustomerService>();
builder.Services.AddScoped<MorningBrew.Api.Services.Interfaces.ILoyaltyService, MorningBrew.Api.Services.Implementations.LoyaltyService>();
builder.Services.AddScoped<MorningBrew.Api.Services.Interfaces.IPromotionService, MorningBrew.Api.Services.Implementations.PromotionService>();
builder.Services.AddScoped<MorningBrew.Api.Services.Interfaces.IFeedbackService, MorningBrew.Api.Services.Implementations.FeedbackService>();
builder.Services.AddScoped<MorningBrew.Api.Services.Interfaces.ISurveyService, MorningBrew.Api.Services.Implementations.SurveyService>();
builder.Services.AddScoped<MorningBrew.Api.Services.Interfaces.ICrmStatsService, MorningBrew.Api.Services.Implementations.CrmStatsService>();

// Đăng ký AuthService
builder.Services.AddScoped<MorningBrew.Api.Services.Interfaces.IAuthService, MorningBrew.Api.Services.Implementations.AuthService>();

var app = builder.Build();

// Dùng Middleware xử lý lỗi (phải ở trên cùng để bắt mọi lỗi)
app.UseMiddleware<GlobalExceptionMiddleware>();

// 4. Bật Swagger ở tất cả môi trường
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Morning Brew API B2 v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// QUAN TRỌNG: UseAuthentication phải đặt trước UseAuthorization
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

// 5. Thử Seed Data an toàn
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetService<DbContext>();
    if (db != null)
    {
        MorningBrew.Api.Data.DbInitializer.Seed(db);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Seed Data Warning]: {ex.Message}");
}

app.Run();
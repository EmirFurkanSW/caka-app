using System.Text;
using CAKA.Api.Data;
using CAKA.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Render.com: PORT ve DATABASE_URL ortam değişkenleri kullanılır
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls("http://0.0.0.0:" + port);

// Render.com DATABASE_URL veya appsettings'teki connection string kullanılır
var connectionString = builder.Configuration.GetConnectionString("Default");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    try
    {
        // postgres:// veya postgresql://; şifrede : olabilir, sadece ilk : ile böl
        var url = databaseUrl.Trim();
        if (url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            url = "postgresql://" + url.Substring(11);
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var dbPort = uri.Port > 0 ? uri.Port : 5432;
        connectionString = $"Host={uri.Host};Port={dbPort};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo.Length > 1 ? userInfo[1] : "")};SSL Mode=Require;Trust Server Certificate=true;";
    }
    catch (Exception ex)
    {
        Console.WriteLine("DATABASE_URL parse error: " + ex.Message);
    }
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Host="))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString ?? "Data Source=caka.db");
});

builder.Services.AddScoped<IAuthTokenService, AuthTokenService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "CAKA-Jwt-Secret-Key-Min-32-Chars-Long!!";
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
    options.AddPolicy("AdminOrPersonel", p => p.RequireRole("Admin", "Personel"));
});

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await SeedData.EnsureAdminAsync(db);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

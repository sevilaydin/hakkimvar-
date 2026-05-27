using Hakkimvar.Data;
using Hakkimvar.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ── Database ─────────────────────────────────────────────────────────────────
var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(dbUrl))
{
    // Render PostgreSQL — DATABASE_URL env var ile otomatik aktif
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseNpgsql(dbUrl));
}
else
{
    // Yerel geliştirme / Render'da DATABASE_URL yoksa SQLite kullan
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite("Data Source=hakkimvar.db"));
}

// ── Cache ─────────────────────────────────────────────────────────────────────
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
if (!string.IsNullOrEmpty(redisUrl))
    builder.Services.AddStackExchangeRedisCache(opt => opt.Configuration = redisUrl);
else
    builder.Services.AddDistributedMemoryCache(); // Redis yoksa RAM cache

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSingleton<ClaudeService>();
builder.Services.AddSingleton<YargitayService>();
builder.Services.AddSingleton<AnalyticsService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ── Rate limiting — IP başına dakikada 15 istek ───────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("chat", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            reply   = "Çok fazla istek gönderdiniz. Lütfen 1 dakika bekleyiniz.",
            sources = Array.Empty<object>()
        });
    };
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── DB Migration — uygulama başlarken tabloları oluştur ───────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // Migration yoksa şemayı direkt oluşturur
}

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseRateLimiter();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();

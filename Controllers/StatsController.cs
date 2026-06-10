using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hakkimvar.Controllers;

[ApiController]
public class StatsController : ControllerBase
{
    private readonly AnalyticsService _analytics;
    private readonly string _statsKey;

    public StatsController(AnalyticsService analytics, IConfiguration configuration)
    {
        _analytics = analytics;
        _statsKey  = configuration["Stats:ApiKey"] ?? "";
    }

    // GET /stats — canlı istatistikler
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        if (!string.IsNullOrWhiteSpace(_statsKey))
        {
            var provided = Request.Headers["X-Stats-Key"].FirstOrDefault() ?? "";
            if (provided != _statsKey)
                return Unauthorized(new { error = "Geçersiz veya eksik X-Stats-Key header." });
        }

        return Ok(_analytics.GetStats());
    }
}

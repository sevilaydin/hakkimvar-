using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hakkimvar.Controllers;

[ApiController]
public class StatsController : ControllerBase
{
    private readonly AnalyticsService _analytics;

    public StatsController(AnalyticsService analytics) => _analytics = analytics;

    // GET /stats — canlı istatistikler
    [HttpGet("stats")]
    public IActionResult GetStats() => Ok(_analytics.GetStats());
}

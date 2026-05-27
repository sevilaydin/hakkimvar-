using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hakkimvar.Controllers;

[ApiController]
public class StatsController(AnalyticsService analytics, IQuestionService questionService)
    : ControllerBase
{
    // GET /stats — canlı istatistikler (in-memory + DB)
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var memStats = analytics.GetStats();
        var dbStats  = await questionService.GetCategoryStatsAsync(30);

        return Ok(new
        {
            live      = memStats,
            db_30days = new { by_category = dbStats.ToDictionary(x => x.Category, x => x.Count) }
        });
    }

    // GET /stats/questions — son sorular (DB'den kalıcı)
    [HttpGet("stats/questions")]
    public async Task<IActionResult> GetQuestions([FromQuery] string? category, [FromQuery] int count = 50)
    {
        var questions = category != null
            ? await questionService.GetByCategoryAsync(category, count)
            : await questionService.GetRecentAsync(count);

        return Ok(questions.Select(q => new
        {
            q.Id, q.Category,
            preview    = q.Text.Length > 120 ? q.Text[..120] + "…" : q.Text,
            q.Helpful,
            q.ResponseMs,
            asked_at   = q.CreatedAt.ToString("dd.MM HH:mm")
        }));
    }
}

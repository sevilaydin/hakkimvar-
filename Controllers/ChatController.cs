using Hakkimvar.Models;
using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hakkimvar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly GroqService       _claudeService;
    private readonly YargitayService   _yargitayService;
    private readonly AnalyticsService  _analytics;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        GroqService claudeService,
        YargitayService yargitayService,
        AnalyticsService analytics,
        ILogger<ChatController> logger)
    {
        _claudeService   = claudeService;
        _yargitayService = yargitayService;
        _analytics       = analytics;
        _logger          = logger;
    }

    [HttpPost]
    [EnableRateLimiting("chat")]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new ChatResponse { Success = false, Error = "Mesaj boş olamaz." });

        var category = CategoryService.Detect(request.Message);
        var preview  = request.Message.Length > 80 ? request.Message[..80] + "…" : request.Message;
        _logger.LogInformation("[{Category}] Soru: {Preview}", category, preview);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var claudeTask   = _claudeService.GetResponseAsync(request.Message, category);
        var yargitayTask = _yargitayService.SearchAsync(request.Message);

        await Task.WhenAll(claudeTask, yargitayTask);

        sw.Stop();
        var (reply, claudeSources, isError) = claudeTask.Result;
        var yargitaySources = yargitayTask.Result;
        var allSources = claudeSources.Concat(yargitaySources).ToList();

        var ms = (int)sw.ElapsedMilliseconds;
        if (isError)
        {
            _analytics.TrackError(category, request.Message, ms);
            _logger.LogWarning("[{Category}] Hata ({Ms}ms): {Reply}", category, ms, reply);
        }
        else
        {
            _analytics.TrackQuestion(category, request.Message, ms);
            _logger.LogInformation("[{Category}] Yanıt ({Ms}ms, {Count} kaynak)", category, ms, allSources.Count);
        }

        return Ok(new ChatResponse { Reply = reply, Success = !isError, Sources = allSources });
    }
}

using Hakkimvar.Models;
using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Hakkimvar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(
    ClaudeService       claudeService,
    YargitayService     yargitayService,
    AnalyticsService    analytics,
    IQuestionService    questionService,
    IDistributedCache   cache,
    ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("chat")]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new ChatResponse { Success = false, Error = "Mesaj boş olamaz." });

        var category = CategoryService.Detect(request.Message);
        var preview  = request.Message.Length > 80 ? request.Message[..80] + "…" : request.Message;
        logger.LogInformation("[{Category}] Soru: {Preview}", category, preview);

        // Cache kontrolü
        var cacheKey = $"chat:{Convert.ToHexString(System.Security.Cryptography.MD5.HashData(
                            System.Text.Encoding.UTF8.GetBytes(request.Message.ToLowerInvariant().Trim())))}";

        var cached = await cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            logger.LogInformation("[{Category}] Cache hit", category);
            var cachedResponse = JsonSerializer.Deserialize<ChatResponse>(cached);
            return Ok(cachedResponse);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var claudeTask   = claudeService.GetResponseAsync(request.Message);
        var yargitayTask = yargitayService.SearchAsync(request.Message);

        await Task.WhenAll(claudeTask, yargitayTask);

        sw.Stop();
        var (reply, claudeSources, isError) = claudeTask.Result;
        var yargitaySources = yargitayTask.Result;
        var allSources = claudeSources.Concat(yargitaySources).ToList();
        var ms = (int)sw.ElapsedMilliseconds;

        if (isError)
        {
            analytics.TrackError(category, request.Message, ms);
            logger.LogWarning("[{Category}] Hata ({Ms}ms): {Reply}", category, ms, reply);
        }
        else
        {
            analytics.TrackQuestion(category, request.Message, ms);
            logger.LogInformation("[{Category}] Yanıt ({Ms}ms, {Count} kaynak)", category, ms, allSources.Count);

            // DB'ye kaydet (arka planda, akışı bloke etmez)
            _ = questionService.SaveAsync(request.Message, category, reply, allSources.Count, ms);

            // 5 dakika cache'le
            var response = new ChatResponse { Reply = reply, Success = true, Sources = allSources };
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
        }

        return Ok(new ChatResponse { Reply = reply, Success = !isError, Sources = allSources });
    }

    // POST /api/chat/rate  — kullanıcı yanıtı oylar
    [HttpPost("rate")]
    public async Task<IActionResult> Rate([FromBody] RateRequest req)
    {
        var ok = await questionService.RateAsync(req.Id, req.Stars);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

public record RateRequest(int Id, int Stars);

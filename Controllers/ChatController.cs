using Hakkimvar.Models;
using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hakkimvar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ClaudeService _claudeService;
    private readonly YargitayService _yargitayService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ClaudeService claudeService, YargitayService yargitayService, ILogger<ChatController> logger)
    {
        _claudeService = claudeService;
        _yargitayService = yargitayService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("chat")]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new ChatResponse { Success = false, Error = "Mesaj boş olamaz." });

        var preview = request.Message.Length > 80 ? request.Message[..80] + "…" : request.Message;
        _logger.LogInformation("Soru alındı: {Preview}", preview);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var claudeTask   = _claudeService.GetResponseAsync(request.Message);
        var yargitayTask = _yargitayService.SearchAsync(request.Message);

        await Task.WhenAll(claudeTask, yargitayTask);

        sw.Stop();
        var (reply, claudeSources, isError) = claudeTask.Result;
        var yargitaySources = yargitayTask.Result;
        var allSources = claudeSources.Concat(yargitaySources).ToList();

        if (isError)
            _logger.LogWarning("Hata yanıtı ({Ms}ms): {Reply}", sw.ElapsedMilliseconds, reply);
        else
            _logger.LogInformation("Yanıt gönderildi ({Ms}ms, {Count} kaynak)", sw.ElapsedMilliseconds, allSources.Count);

        return Ok(new ChatResponse { Reply = reply, Success = !isError, Sources = allSources });
    }
}

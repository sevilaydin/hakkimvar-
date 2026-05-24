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

        var claudeTask = _claudeService.GetResponseAsync(request.Message);
        var yargitayTask = _yargitayService.SearchAsync(request.Message);

        await Task.WhenAll(claudeTask, yargitayTask);

        var (reply, claudeSources, isError) = claudeTask.Result;
        var yargitaySources = yargitayTask.Result;

        var allSources = claudeSources.Concat(yargitaySources).ToList();

        return Ok(new ChatResponse { Reply = reply, Success = !isError, Sources = allSources });
    }
}

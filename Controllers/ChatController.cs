using Hakkimvar.Models;
using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hakkimvar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ClaudeService _claudeService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ClaudeService claudeService, ILogger<ChatController> logger)
    {
        _claudeService = claudeService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new ChatResponse { Success = false, Error = "Mesaj boş olamaz." });

        var (reply, sources, isError) = await _claudeService.GetResponseAsync(request.Message);
        return Ok(new ChatResponse { Reply = reply, Success = !isError, Sources = sources });
    }
}

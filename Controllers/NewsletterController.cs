using Hakkimvar.Data;
using Hakkimvar.Models;
using Hakkimvar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hakkimvar.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsletterController(AppDbContext db, IEmailService email, ILogger<NewsletterController> logger)
    : ControllerBase
{
    // POST /api/newsletter/subscribe
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return BadRequest(new { success = false, message = "Geçerli bir email adresi giriniz." });

        var existing = await db.NewsletterSubscribers.FirstOrDefaultAsync(n => n.Email == req.Email);
        if (existing != null)
        {
            if (existing.IsActive)
                return Ok(new { success = true, message = "Bu email zaten abone." });

            existing.IsActive = true;
            existing.SubscribedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await email.SendWelcomeAsync(req.Email);
            return Ok(new { success = true, message = "Aboneliğiniz yeniden aktif edildi." });
        }

        var subscriber = new NewsletterSubscriber { Email = req.Email };
        db.NewsletterSubscribers.Add(subscriber);
        await db.SaveChangesAsync();

        await email.SendWelcomeAsync(req.Email);
        logger.LogInformation("Newsletter abonesi: {Email}", req.Email);

        return Ok(new { success = true, message = "Abone oldunuz! Teşekkürler." });
    }

    // GET /api/newsletter/unsubscribe/{token}
    [HttpGet("unsubscribe/{token}")]
    public async Task<IActionResult> Unsubscribe(string token)
    {
        var subscriber = await db.NewsletterSubscribers
            .FirstOrDefaultAsync(n => n.UnsubscribeToken == token);

        if (subscriber == null)
            return Content("<h2>Link geçersiz.</h2>", "text/html");

        subscriber.IsActive = false;
        await db.SaveChangesAsync();

        logger.LogInformation("Newsletter aboneliği iptal: {Email}", subscriber.Email);
        return Content("""
            <html><body style="font-family:sans-serif;text-align:center;padding:60px">
            <h2>⚖️ Aboneliğiniz iptal edildi.</h2>
            <p>Artık HakkımVar bülteni almayacaksınız.</p>
            <a href="https://hakkimvar.onrender.com">Ana sayfaya dön</a>
            </body></html>
            """, "text/html");
    }

    // GET /api/newsletter/count  (admin için)
    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        var active = await db.NewsletterSubscribers.CountAsync(n => n.IsActive);
        return Ok(new { active_subscribers = active });
    }
}

public record SubscribeRequest(string? Email);

using Microsoft.AspNetCore.Mvc;

namespace Hakkimvar.Controllers;

/// <summary>
/// Stripe ödeme entegrasyonu.
/// Aktif etmek için:
///   1. stripe.com üzerinde hesap aç
///   2. Render env var olarak STRIPE__SECRETKEY ve STRIPE__WEBHOOKSECRET ekle
///   3. Bu dosyadaki TODO yorum satırlarını kaldır
///   4. dotnet add package Stripe.net
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<SubscriptionController> _logger;

    private static readonly Dictionary<string, (string Name, int PriceTRY)> Plans = new()
    {
        ["monthly"] = ("HakkımVar Premium - 1 Ay",  149),
        ["yearly"]  = ("HakkımVar Premium - 1 Yıl", 999),
    };

    public SubscriptionController(IConfiguration config, ILogger<SubscriptionController> logger)
    {
        _config = config;
        _logger = logger;
    }

    // POST /api/subscription/checkout
    [HttpPost("checkout")]
    public IActionResult CreateCheckout([FromBody] CheckoutRequest req)
    {
        var stripeKey = _config["Stripe:SecretKey"];

        if (string.IsNullOrWhiteSpace(stripeKey))
            return StatusCode(503, new { error = "Ödeme sistemi henüz aktif değil." });

        if (!Plans.TryGetValue(req.Plan ?? "monthly", out var plan))
            return BadRequest(new { error = "Geçersiz plan." });

        // TODO: Stripe.net paketi eklenince aşağıdaki kodu aktif et
        // StripeConfiguration.ApiKey = stripeKey;
        // var options = new SessionCreateOptions
        // {
        //     SuccessUrl        = $"{req.BaseUrl}/odeme-basarili?session_id={{CHECKOUT_SESSION_ID}}",
        //     CancelUrl         = $"{req.BaseUrl}/?odeme=iptal",
        //     PaymentMethodTypes = ["card"],
        //     Mode              = "payment",
        //     CustomerEmail     = req.Email,
        //     LineItems =
        //     [
        //         new SessionLineItemOptions
        //         {
        //             PriceData = new SessionLineItemPriceDataOptions
        //             {
        //                 UnitAmount  = plan.PriceTRY * 100L,
        //                 Currency    = "try",
        //                 ProductData = new() { Name = plan.Name }
        //             },
        //             Quantity = 1
        //         }
        //     ]
        // };
        // var session = await new SessionService().CreateAsync(options);
        // return Ok(new { checkoutUrl = session.Url });

        _logger.LogInformation("Ödeme isteği: {Plan} / {Email}", req.Plan, req.Email);
        return StatusCode(503, new { error = "Ödeme sistemi yakında aktif olacak." });
    }

    // POST /api/subscription/webhook  (Stripe'tan gelen ödeme bildirimleri)
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var webhookSecret = _config["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
            return Ok();

        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        // TODO: Stripe.net paketi eklenince aktif et
        // try
        // {
        //     var stripeEvent = EventUtility.ConstructEvent(json,
        //         Request.Headers["Stripe-Signature"], webhookSecret);
        //
        //     if (stripeEvent.Type == "checkout.session.completed")
        //     {
        //         var session = (Session)stripeEvent.Data.Object;
        //         _logger.LogInformation("Ödeme onaylandı: {Email}", session.CustomerEmail);
        //         // TODO: kullanıcıyı premium yap (DB Phase'ında)
        //     }
        // }
        // catch (StripeException ex)
        // {
        //     _logger.LogError(ex, "Stripe webhook hatası");
        //     return BadRequest();
        // }

        return Ok();
    }
}

public record CheckoutRequest(string? Email, string? Plan, string? BaseUrl);

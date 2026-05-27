namespace Hakkimvar.Models;

public class Subscription
{
    public int      Id               { get; set; }
    public string   Email            { get; set; } = string.Empty;
    public string   StripeCustomerId { get; set; } = string.Empty;
    public string   StripeSessionId  { get; set; } = string.Empty;
    public string   PlanType         { get; set; } = "premium";  // premium / free
    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
    public DateTime PaidUntil        { get; set; }

    public bool IsActive => PaidUntil > DateTime.UtcNow;
}

namespace Hakkimvar.Models;

public class NewsletterSubscriber
{
    public int      Id               { get; set; }
    public string   Email            { get; set; } = string.Empty;
    public string   UnsubscribeToken { get; set; } = Guid.NewGuid().ToString("N");
    public bool     IsActive         { get; set; } = true;
    public DateTime SubscribedAt     { get; set; } = DateTime.UtcNow;
}

using SendGrid;
using SendGrid.Helpers.Mail;

namespace Hakkimvar.Services;

public interface IEmailService
{
    Task SendWelcomeAsync(string toEmail);
    Task SendAsync(string toEmail, string subject, string htmlBody);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    private readonly string? _apiKey   = config["SendGrid:ApiKey"];
    private const string     FromEmail = "noreply@hakkimvar.com";
    private const string     FromName  = "HakkımVar";

    public Task SendWelcomeAsync(string toEmail) => SendAsync(
        toEmail,
        "HakkımVar bültenine hoş geldiniz ⚖️",
        """
        <p>Merhaba,</p>
        <p>HakkımVar haber bültenine abone olduğunuz için teşekkürler.</p>
        <p>Yeni emsal kararlar ve kanun değişiklikleri hakkında sizi bilgilendireceğiz.</p>
        <br/>
        <p><small><a href="https://hakkimvar.onrender.com/api/newsletter/unsubscribe/{token}">Abonelikten çık</a></small></p>
        """);

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("SendGrid API key yok — email gönderilmedi: {To}", toEmail);
            return;
        }

        try
        {
            var client  = new SendGridClient(_apiKey);
            var from    = new EmailAddress(FromEmail, FromName);
            var to      = new EmailAddress(toEmail);
            var msg     = MailHelper.CreateSingleEmail(from, to, subject, null, htmlBody);
            var res     = await client.SendEmailAsync(msg);

            if (!res.IsSuccessStatusCode)
                logger.LogWarning("SendGrid {Status}: {To}", res.StatusCode, toEmail);
            else
                logger.LogInformation("Email gönderildi: {To}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Email gönderilemedi: {To}", toEmail);
        }
    }
}

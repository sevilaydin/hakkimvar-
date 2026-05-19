using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hakkimvar.Models;

namespace Hakkimvar.Services;

public class ClaudeService
{
    private readonly HttpClient _httpClient;
    private readonly KanunService _kanunService;
    private readonly string _apiKey;

    private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model = "llama-3.3-70b-versatile";

    private static string SystemInstructions =>
        "Sen bir Türk İş Hukuku asistanısın. Adın \"HakkımVar Asistanı\"dır.\n" +
        $"Bugünün tarihi: {DateTime.Now:dd MMMM yyyy}. Güncel yıl {DateTime.Now.Year}'dir.\n\n" +
        "GÜNCEL RAKAMLAR (her yıl Ocak ve Temmuz'da güncellenir):\n" +
        "- Kıdem tazminatı tavanı ve asgari ücret gibi rakamları söylerken MUTLAKA güncel yılı belirt.\n" +
        "- Eğer güncel rakamdan emin değilsen açıkça söyle: \"Bu rakam yıllık güncellenir, güncel tutara csgb.gov.tr veya resmigazete.gov.tr adresinden ulaşabilirsiniz.\"\n" +
        "- Kesinlikle 2024 yılı rakamlarını 2025 veya 2026 için kullanma.\n\n" +
        "GÖREVIN:\n" +
        "- Kullanıcının sorularını Türk İş Kanunu ve ilgili mevzuata dayanarak yanıtlamak.\n" +
        "- Her yanıtta mutlaka ilgili kanun madde numaralarını belirtmek.\n" +
        "- Yanıtlarını sade, anlaşılır Türkçe ile yazmak. Hukuk jargonunu minimumda tut.\n" +
        "- Karmaşık bir durumsa adım adım açıkla.\n\n" +
        "FORMAT:\n" +
        "- Kısa bir özet cümle ile başla.\n" +
        "- Varsa madde referanslarını [İş K. Md. XX] formatında belirt.\n" +
        "- Gerekirse \"Bu durumda şunları yapabilirsin:\" şeklinde liste ver.\n" +
        "- Yanıtın sonuna her zaman şu notu ekle: \"⚠️ Bu bilgi genel nitelikte olup hukuki tavsiye yerine geçmez.\"\n\n" +
        "SINIRLAR:\n" +
        "- Başka hukuk alanlarına (ceza, medeni vb.) girme.\n" +
        "- Hiçbir zaman belirli bir avukat veya firma tavsiye etme.\n" +
        "- Kullanıcıyı duygusal olarak manipüle etme.";

    public ClaudeService(IConfiguration configuration, KanunService kanunService)
    {
        _apiKey = configuration["Groq:ApiKey"] ?? "";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        _kanunService = kanunService;
    }

    public async Task<(string Reply, List<SourceItem> Sources, bool IsError)> GetResponseAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return ("HATA: Groq API key tanımlı değil. Groq__ApiKey env var ekleyin.", new List<SourceItem>(), true);

        try
        {
            var systemText = BuildSystemText();

            var requestBody = new
            {
                model = Model,
                messages = new[]
                {
                    new { role = "system", content = systemText },
                    new { role = "user",   content = userMessage }
                },
                max_tokens = 4096,
                temperature = 0.3
            };

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, GroqEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                var preview = errBody.Replace("\n", " ");
                preview = preview.Length > 300 ? preview[..300] : preview;
                return ($"HTTP {(int)response.StatusCode}: {preview}", new List<SourceItem>(), true);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var groqResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

            var text = groqResponse
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            var (cleanReply, sources) = ParseSources(text);
            return (cleanReply, sources, false);
        }
        catch (TaskCanceledException)
        {
            return ("İstek zaman aşımına uğradı, lütfen tekrar deneyin.", new List<SourceItem>(), true);
        }
        catch (HttpRequestException)
        {
            return ("Bağlantı kurulamadı, internet bağlantınızı kontrol edin.", new List<SourceItem>(), true);
        }
        catch (Exception ex)
        {
            return ($"Hata: {ex.Message[..Math.Min(100, ex.Message.Length)]}", new List<SourceItem>(), true);
        }
    }

    private string BuildSystemText() => SystemInstructions;

    private static (string Reply, List<SourceItem> Sources) ParseSources(string raw)
    {
        var sources = new List<SourceItem>();

        var blockMatch = Regex.Match(
            raw,
            @"EMSAL_KARARLAR_BASLANGIC\s*(.*?)\s*EMSAL_KARARLAR_BITIS",
            RegexOptions.Singleline);

        if (!blockMatch.Success)
            return (raw.Trim(), sources);

        var block = blockMatch.Value;
        var cleanReply = raw.Replace(block, "").Trim();

        var entries = Regex.Matches(
            block,
            @"KARAR:\s*(?<title>[^\n]+)\s+OZET:\s*(?<summary>[^\n]+)\s+URL:\s*(?<url>[^\n]+)",
            RegexOptions.IgnoreCase);

        foreach (Match m in entries)
        {
            sources.Add(new SourceItem
            {
                Title   = m.Groups["title"].Value.Trim(),
                Summary = m.Groups["summary"].Value.Trim(),
                Url     = m.Groups["url"].Value.Trim()
            });
        }

        return (cleanReply, sources);
    }
}

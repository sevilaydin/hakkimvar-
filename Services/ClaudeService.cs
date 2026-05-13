using System.Net.Http;
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

    private const string GeminiEndpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

    private const string SystemInstructions =
        "Sen bir Türk İş Hukuku asistanısın. Adın \"HakkımVar Asistanı\"dır.\n\n" +
        "GÖREVIN:\n" +
        "- Kullanıcının sorularını yalnızca sana verilen İş Kanunu metni ve ilgili mevzuata dayanarak yanıtlamak.\n" +
        "- Kıdem tazminatı tavanı, asgari ücret, SGK primleri gibi yıllık değişen rakamları güncel bilgilerine göre ver.\n" +
        "- Her yanıtta mutlaka ilgili kanun madde numaralarını belirtmek.\n" +
        "- Yanıtlarını sade, anlaşılır Türkçe ile yazmak. Hukuk jargonunu minimumda tut.\n" +
        "- Karmaşık bir durumsa adım adım açıkla.\n\n" +
        "FORMAT:\n" +
        "- Kısa bir özet cümle ile başla.\n" +
        "- Varsa madde referanslarını [İş K. Md. XX] formatında belirt.\n" +
        "- Gerekirse \"Bu durumda şunları yapabilirsin:\" şeklinde liste ver.\n" +
        "- Yanıtın sonuna her zaman şu notu ekle: \"⚠️ Bu bilgi genel nitelikte olup hukuki tavsiye yerine geçmez.\"\n\n" +
        "SINIRLAR:\n" +
        "- Kanun metninde karşılığı olmayan konularda yorum yapma, \"Bu konuda kesin bilgi veremem\" de.\n" +
        "- Başka hukuk alanlarına (ceza, medeni vb.) girme.\n" +
        "- Hiçbir zaman belirli bir avukat veya firma tavsiye etme.\n" +
        "- Kullanıcıyı duygusal olarak manipüle etme.";

    public ClaudeService(IConfiguration configuration, KanunService kanunService)
    {
        _apiKey = configuration["Gemini:ApiKey"] ?? "";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        _kanunService = kanunService;
    }

    public async Task<(string Reply, List<SourceItem> Sources, bool IsError)> GetResponseAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return ("HATA: Gemini API key Render'da tanımlı değil. Gemini__ApiKey env var ekleyin.", new List<SourceItem>(), true);

        try
        {
            var systemText = BuildSystemText();

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemText } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userMessage } }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 4096,
                    temperature = 0.3
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{GeminiEndpoint}?key={_apiKey}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                var preview = errBody.Replace("\n", " ").Replace("\r", "");
                preview = preview.Length > 400 ? preview[..400] : preview;
                return ($"HTTP {(int)response.StatusCode}: {preview}", new List<SourceItem>(), true);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var geminiResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

            var text = geminiResponse
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
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
        catch
        {
            return ("Sunucu hatası oluştu, lütfen tekrar deneyin.", new List<SourceItem>(), true);
        }
    }

    private string BuildSystemText()
    {
        if (!_kanunService.HasKanunMetni())
            return SystemInstructions;
        return SystemInstructions + "\n\nKANUN METNİ BAĞLAMI:\n\n" + _kanunService.GetKanunMetni();
    }

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

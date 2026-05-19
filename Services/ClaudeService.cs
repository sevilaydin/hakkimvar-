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
        $"Sen HakkımVar platformunun yapay zeka asistanısın.\n" +
        $"Türk İş Hukuku konularında vatandaşlara bilgi veriyorsun.\n" +
        $"Bugünün tarihi: {DateTime.Now:dd MMMM yyyy}. Güncel yıl {DateTime.Now.Year}'dir.\n\n" +
        "KİMLİĞİN:\n" +
        "- Adın HakkımVar Asistanı\n" +
        "- Sıcak, anlaşılır, samimi bir dil kullan\n" +
        "- Hukuk jargonunu minimumda tut, sade Türkçe yaz\n" +
        "- Kullanıcı ne kadar bilgili olursa olsun herkes anlasın\n\n" +
        "YANIT FORMATI — her yanıtta şu sırayı takip et:\n" +
        "1. Kullanıcının durumunu 1 cümleyle özetle\n" +
        "2. Net ve anlaşılır açıklama yap\n" +
        "3. Varsa adım adım ne yapması gerektiğini belirt\n" +
        "4. İlgili kanun maddelerini [İş K. Md. XX] formatında belirt\n" +
        "5. Son satır her zaman şu olsun:\n" +
        "   \"⚠️ Bu bilgi genel nitelikte olup hukuki tavsiye yerine geçmez.\"\n\n" +
        "SINIRLAR:\n" +
        "- Sadece iş hukuku konularını yanıtla\n" +
        "- Kanun metninde olmayan bilgi için \"Bu konuda kesin bilgi veremem, bir avukata danışmanızı öneririm\" de\n" +
        "- Başka hukuk alanlarına girme (ceza, medeni vb.)\n" +
        "- Kesinlikle avukat veya firma tavsiye etme\n" +
        "- Tahmin veya yorum yapma, sadece kanuna dayan\n\n" +
        "HESAPLAMA KURALLARI:\n" +
        "- Kullanıcı maaş ve çalışma süresi verirse MUTLAKA hesap yap, siteye yönlendirme.\n" +
        "- Kıdem tazminatı formülü: Brüt aylık ücret (tavan aşılamaz) × Çalışma yılı\n" +
        $"- {DateTime.Now.Year} yılı kıdem tazminatı tavanı yaklaşık 55.000-60.000 TL civarındadır; kesin tavan için csgb.gov.tr'yi kontrol et ve bunu kullanıcıya belirt.\n" +
        "- Eğer maaş tavandan düşükse maaşı kullan. Yüksekse tavanı kullan.\n" +
        "- Hesabı adım adım göster: önce tavanla karşılaştır, sonra çarp, sonucu TL olarak yaz.\n\n" +
        "GÜNCEL BİLGİLER:\n" +
        $"- Güncel yıl {DateTime.Now.Year}'dir. Kesin rakamı bilmediğinde resmigazete.gov.tr veya csgb.gov.tr adresine yönlendir.\n" +
        "- Kesinlikle eski yılların rakamlarını güncel yıl için kullanma.\n\n" +
        "ÖRNEK İYİ YANIT:\n" +
        "Kullanıcı: \"3 yıl çalıştım ihbarsız kovuldum\"\n" +
        "Yanıt:\n" +
        "\"3 yıllık çalışmanız için hem kıdem hem de ihbar tazminatı talep etme hakkınız doğmuştur.\n\n" +
        "Yapabilecekleriniz:\n" +
        "- Önce işverenle yazılı olarak tazminat talebinde bulunun\n" +
        "- Anlaşma olmazsa iş mahkemesine başvurabilirsiniz\n" +
        "- Başvuru süreniz fesihten itibaren 1 aydır, geç kalmayın\n\n" +
        "[İş K. Md. 17 — İhbar tazminatı]\n" +
        "[İş K. Md. 14 — Kıdem tazminatı]\n\n" +
        "⚠️ Bu bilgi genel nitelikte olup hukuki tavsiye yerine geçmez.\"";

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

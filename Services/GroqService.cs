using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hakkimvar.Models;

namespace Hakkimvar.Services;

public class GroqService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _kanunMetni;

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
        "- 2026 yılı kıdem tazminatı tavanı (01.01.2026-30.06.2026): 64.948,77 TL (kesin rakam, Hazine ve Maliye Bakanlığı Genelgesi 06.01.2026)\n" +
        "- Eğer maaş tavandan düşükse maaşı kullan. Yüksekse tavanı (64.948,77 TL) kullan.\n" +
        "- Hesabı adım adım göster: önce tavanla karşılaştır, sonra çarp, sonucu TL olarak net yaz.\n" +
        "- Kesinlikle 'yaklaşık', 'civarında', 'farzedelim ki' gibi belirsiz ifadeler kullanma. Tavan kesin: 64.948,77 TL.\n\n" +
        "GÜNCEL BİLGİLER:\n" +
        $"- Güncel yıl {DateTime.Now.Year}'dir.\n" +
        "- 2026 kıdem tazminatı tavanı: 64.948,77 TL (Ocak-Haziran 2026). Bu rakamı kullan.\n\n" +
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

    public GroqService(IConfiguration configuration, KanunService kanunService)
    {
        _apiKey    = configuration["Groq:ApiKey"] ?? "";
        _kanunMetni = kanunService.GetKanunMetni();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
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
                max_tokens = 1500,
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

            return (text.Trim(), new List<SourceItem>(), false);
        }
        catch (TaskCanceledException)
        {
            return ("Yanıt zaman aşımına uğradı, lütfen tekrar deneyin.", new List<SourceItem>(), true);
        }
        catch (HttpRequestException)
        {
            return ("Sunucuya bağlanılamadı, lütfen tekrar deneyin.", new List<SourceItem>(), true);
        }
        catch (Exception)
        {
            return ("Beklenmeyen bir hata oluştu, lütfen tekrar deneyin.", new List<SourceItem>(), true);
        }
    }

    private string BuildSystemText()
    {
        if (string.IsNullOrWhiteSpace(_kanunMetni))
            return SystemInstructions;

        return SystemInstructions +
               "\n\n---\nAŞAĞIDA REFERANS OLARAK 4857 SAYILI İŞ KANUNU TAM METNİ VERİLMİŞTİR.\n" +
               "Yanıt verirken bu metni birincil kaynak olarak kullan.\n---\n" +
               _kanunMetni;
    }
}

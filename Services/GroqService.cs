using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hakkimvar.Models;

namespace Hakkimvar.Services;

public class GroqService
{
    private readonly HttpClient   _httpClient;
    private readonly string       _apiKey;
    private readonly KanunService _kanunService;
    private readonly decimal      _kidemTavan;
    private readonly string       _kidemDonem;
    private readonly string       _kidemYururluk;

    private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model = "llama-3.3-70b-versatile";

    private string SystemInstructions =>
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
        "- Kıdem tazminatı formülü: Giydirilmiş brüt aylık ücret (tavan aşılamaz) × Çalışma yılı\n" +
        "- Giydirilmiş brüt ücret: çıplak brüt maaş + yol, yemek, ikramiye gibi nakdi yan haklar\n" +
        $"- {DateTime.Now.Year} yılı kıdem tazminatı tavanı ({_kidemDonem}, {_kidemYururluk} itibarıyla): {_kidemTavan:N2} TL (kesin rakam, Hazine ve Maliye Bakanlığı Genelgesi)\n" +
        $"- Eğer giydirilmiş brüt ücret tavandan düşükse ücreti kullan. Yüksekse tavanı ({_kidemTavan:N2} TL) kullan.\n" +
        "- İhbar tazminatı formülü: Günlük brüt ücret (brüt aylık / 30) × İhbar süresi (gün olarak)\n" +
        "- İhbar süreleri (KESİN, İş K. Md.17):\n" +
        "    * 0-6 ay kıdem       → 2 hafta = 14 gün\n" +
        "    * 6 ay – 1,5 yıl    → 4 hafta = 28 gün\n" +
        "    * 1,5 yıl – 3 yıl   → 6 hafta = 42 gün\n" +
        "    * 3 yıldan fazla     → 8 hafta = 56 gün  ← 3 yıl üstü için bu\n" +
        "- Hesabı adım adım göster: önce tavanla karşılaştır, sonra çarp, sonucu TL olarak net yaz.\n" +
        $"- Kesinlikle 'yaklaşık', 'civarında', 'farzedelim ki' gibi belirsiz ifadeler kullanma. Tavan kesin: {_kidemTavan:N2} TL.\n\n" +
        "GÜNCEL BİLGİLER:\n" +
        $"- Güncel yıl {DateTime.Now.Year}'dir.\n" +
        $"- {DateTime.Now.Year} kıdem tazminatı tavanı: {_kidemTavan:N2} TL ({_kidemDonem}). Bu rakamı kullan.\n\n" +
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
        _apiKey        = configuration["Groq:ApiKey"] ?? "";
        _kanunService  = kanunService;
        _kidemTavan    = configuration.GetValue<decimal>("KidemTazminati:Tavan", 64948.77m);
        _kidemDonem    = configuration["KidemTazminati:Donem"]       ?? "Ocak-Haziran 2026";
        _kidemYururluk = configuration["KidemTazminati:YururlukTarihi"] ?? "01.01.2026";
        _httpClient    = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    public async Task<(string Reply, List<SourceItem> Sources, bool IsError)> GetResponseAsync(string userMessage, string category = "diger")
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return ("HATA: Groq API key tanımlı değil. Groq__ApiKey env var ekleyin.", new List<SourceItem>(), true);

        try
        {
            var systemText = BuildSystemText(category);

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

    private string BuildSystemText(string category)
    {
        var articles = _kanunService.GetArticlesForCategory(category);
        if (string.IsNullOrWhiteSpace(articles))
            return SystemInstructions;

        return SystemInstructions +
               "\n\n---\nAŞAĞIDA BU SORUYLA İLGİLİ 4857 SAYILI İŞ KANUNU MADDELERİ VERİLMİŞTİR.\n" +
               "Yanıt verirken bu maddeleri birincil kaynak olarak kullan.\n---\n" +
               articles;
    }
}

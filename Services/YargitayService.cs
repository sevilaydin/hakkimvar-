using System.Net.Http;
using System.Text.RegularExpressions;
using Hakkimvar.Models;

namespace Hakkimvar.Services;

public class YargitayService
{
    private readonly HttpClient _httpClient;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ve", "ile", "bir", "bu", "da", "de", "ki", "mi", "mı", "mu", "mü",
        "ne", "ben", "sen", "biz", "siz", "için", "olan", "var", "yok",
        "nasıl", "neden", "hangi", "kadar", "sonra", "önce", "gibi",
        "ama", "fakat", "çünkü", "eğer", "ise", "bile", "dahi", "hem",
        "çalışıyorum", "çalışırdım", "işten", "işveren", "işçi"
    };

    public YargitayService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
    }

    public async Task<List<SourceItem>> SearchAsync(string userMessage)
    {
        var keywords = ExtractKeywords(userMessage);
        if (!keywords.Any()) return new();

        var query = string.Join(" ", keywords.Take(3));
        var encodedQuery = Uri.EscapeDataString(query);

        var searchUrl = $"https://karararama.yargitay.gov.tr/YargitayBilgiBankasiIstemciWeb/faces/jsp/ozet_liste.jsp?aranan={encodedQuery}&detay=false";
        var fallback = new List<SourceItem>
        {
            new SourceItem
            {
                Title   = $"Yargıtay Kararları: {query}",
                Summary = $"Yargıtay'ın \"{query}\" konusundaki kararlarını görüntülemek için tıklayın.",
                Url     = searchUrl
            }
        };

        try
        {
            var html = await _httpClient.GetStringAsync(searchUrl);
            var scraped = ParseResults(html);
            return scraped.Count > 0 ? scraped : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static List<SourceItem> ParseResults(string html)
    {
        var results = new List<SourceItem>();

        // Karar satırlarını bul: "9. HD, 2023/1234 E., 2023/5678 K." gibi
        var kararlar = Regex.Matches(html,
            @"(?<hd>\d{1,2}\.\s*(?:H\.?D\.?|C\.?D\.?|HGK|CGK|HD|CD))[^\d<]*(?<esas>\d{4}/\d+)\s*E[^\d<]*(?<karar>\d{4}/\d+)\s*K",
            RegexOptions.IgnoreCase);

        foreach (Match m in kararlar.Take(3))
        {
            var hd    = m.Groups["hd"].Value.Trim();
            var esas  = m.Groups["esas"].Value.Trim();
            var karar = m.Groups["karar"].Value.Trim();
            var title = $"Yargıtay {hd}, {esas} E. {karar} K.";

            // Özet: karar etiketinden sonraki metin
            var afterMatch = html[(m.Index + m.Length)..];
            var summary = Regex.Replace(afterMatch[..Math.Min(300, afterMatch.Length)], "<[^>]+>", "").Trim();
            summary = Regex.Replace(summary, @"\s+", " ");
            summary = summary.Length > 200 ? summary[..200] + "…" : summary;

            results.Add(new SourceItem
            {
                Title   = title,
                Summary = string.IsNullOrWhiteSpace(summary) ? "Yargıtay kararı" : summary,
                Url     = $"https://karararama.yargitay.gov.tr"
            });
        }

        return results;
    }

    private static List<string> ExtractKeywords(string message)
    {
        // Önce bilinen hukuki terimleri yakala
        var legalTerms = new[]
        {
            "kıdem tazminatı", "ihbar tazminatı", "işten çıkarma", "fazla mesai",
            "yıllık izin", "iş kazası", "mobbing", "haksız fesih", "istifa",
            "kötü niyet tazminatı", "ayrımcılık", "sendikal", "grevden çıkarma"
        };

        var found = legalTerms
            .Where(t => message.Contains(t, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (found.Any())
            return found.SelectMany(t => t.Split(' ')).Distinct().ToList();

        // Yoksa genel kelime çıkarımı
        return Regex.Split(message.ToLowerInvariant(), @"[\s,;.!?()\-]+")
            .Where(w => w.Length > 3 && !StopWords.Contains(w))
            .Distinct()
            .Take(4)
            .ToList();
    }
}

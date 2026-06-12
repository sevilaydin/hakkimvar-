using System.Text;
using System.Text.RegularExpressions;

namespace Hakkimvar.Services;

public class KanunService
{
    private readonly string _kanunMetni;
    private readonly Dictionary<int, string> _articles;

    // Kategori → ilgili madde numaraları
    private static readonly Dictionary<string, int[]> CategoryArticles = new()
    {
        ["kidem_tazminati"]  = [14, 17, 26],
        ["ihbar_tazminati"]  = [17, 26, 27],
        ["fazla_mesai"]      = [41, 42, 63],
        ["yillik_izin"]      = [53, 54, 55, 56, 57, 59],
        ["mobbing"]          = [24, 18, 19, 20, 21],
        ["is_kazasi"]        = [24, 25, 77],
        ["istifa"]           = [24, 26, 17],
        ["haksiz_fesih"]     = [17, 18, 19, 20, 21, 22, 24, 25],
        ["ucret_uyusmaz"]    = [32, 34, 35, 39],
        ["emeklilik_sgk"]    = [14, 17],
        ["diger"]            = [1, 2, 8, 17],
    };

    public KanunService(IWebHostEnvironment env)
    {
        var filePath = Path.Combine(env.ContentRootPath, "Data", "is_kanunu.txt");
        _kanunMetni = File.Exists(filePath)
            ? File.ReadAllText(filePath, Encoding.UTF8)
            : string.Empty;
        _articles = ParseArticles(_kanunMetni);
    }

    public string GetKanunMetni() => _kanunMetni;
    public bool HasKanunMetni() => !string.IsNullOrWhiteSpace(_kanunMetni);

    public string GetArticlesForCategory(string category)
    {
        if (_articles.Count == 0) return string.Empty;

        var nums = CategoryArticles.TryGetValue(category, out var n) ? n : CategoryArticles["diger"];
        var sections = nums
            .Where(_articles.ContainsKey)
            .Select(num => _articles[num]);
        return string.Join("\n\n", sections);
    }

    private static Dictionary<int, string> ParseArticles(string text)
    {
        var result = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var matches = Regex.Matches(text,
            @"(MADDE\s+(\d+)\s*[–\-].+?)(?=MADDE\s+\d+\s*[–\-]|\z)",
            RegexOptions.Singleline);

        foreach (Match m in matches)
        {
            if (int.TryParse(m.Groups[2].Value, out var num))
                result[num] = m.Groups[1].Value.Trim();
        }

        return result;
    }
}

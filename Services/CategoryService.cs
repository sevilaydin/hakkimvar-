namespace Hakkimvar.Services;

public static class CategoryService
{
    private static readonly (string Pattern, string Category)[] Rules =
    [
        (@"kıdem|tazminat",              "kidem_tazminati"),
        (@"ihbar|bildirim\s+süresi",     "ihbar_tazminati"),
        (@"fazla\s+mesai|mesai",         "fazla_mesai"),
        (@"izin|yıllık",                 "yillik_izin"),
        (@"mobbing|taciz|psikolojik",    "mobbing"),
        (@"iş\s+kazası|kaza",            "is_kazasi"),
        (@"istifa",                      "istifa"),
        (@"fesih|işten\s+çıkar",         "haksiz_fesih"),
        (@"maaş|ücret",                  "ucret_uyusmaz"),
        (@"emekli|sgk|sigorta",          "emeklilik_sgk"),
    ];

    public static string Detect(string message)
    {
        var lower = message.ToLowerInvariant();
        foreach (var (pattern, category) in Rules)
            if (System.Text.RegularExpressions.Regex.IsMatch(lower, pattern))
                return category;
        return "diger";
    }
}

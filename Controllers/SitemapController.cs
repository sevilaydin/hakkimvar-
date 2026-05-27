using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Hakkimvar.Controllers;

[ApiController]
public class SitemapController : ControllerBase
{
    private const string BaseUrl = "https://hakkimvar.onrender.com";

    [HttpGet("sitemap.xml")]
    public IActionResult GetSitemap()
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        AddUrl(sb, "/",           "daily",   "1.0");
        AddUrl(sb, "/health",     "monthly", "0.1");

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    private static void AddUrl(StringBuilder sb, string path, string freq, string priority)
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{BaseUrl}{path}</loc>");
        sb.AppendLine($"    <lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod>");
        sb.AppendLine($"    <changefreq>{freq}</changefreq>");
        sb.AppendLine($"    <priority>{priority}</priority>");
        sb.AppendLine("  </url>");
    }
}

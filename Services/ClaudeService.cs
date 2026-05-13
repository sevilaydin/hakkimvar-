using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Hakkimvar.Models;

namespace Hakkimvar.Services;

public class ClaudeService
{
    private readonly AnthropicClient _client;
    private readonly KanunService _kanunService;

    private const string SystemInstructions =
        "Sen bir Türk İş Hukuku asistanısın. Adın \"HakkımVar Asistanı\"dır.\n\n" +
        "GÖREVIN:\n" +
        "- Kullanıcının sorularını yalnızca sana verilen İş Kanunu metni ve ilgili mevzuata dayanarak yanıtlamak.\n" +
        "- Kıdem tazminatı tavanı, asgari ücret, SGK primleri gibi yıllık değişen rakamları web'den aratarak bul.\n" +
        "- Arama yaparken 'site:resmigazete.gov.tr' veya 'site:csgb.gov.tr' gibi resmi kaynaklara öncelik ver.\n" +
        "- Kullanıcının sorusuyla ilgili emsal Yargıtay kararı varsa karararama.yargitay.gov.tr veya mevzuat.gov.tr " +
        "üzerinden ara ve yanıtına ekle.\n" +
        "- Her yanıtta mutlaka ilgili kanun madde numaralarını belirtmek.\n" +
        "- Yanıtlarını sade, anlaşılır Türkçe ile yazmak. Hukuk jargonunu minimumda tut.\n" +
        "- Karmaşık bir durumsa adım adım açıkla.\n\n" +
        "FORMAT:\n" +
        "- Kısa bir özet cümle ile başla.\n" +
        "- Varsa madde referanslarını [İş K. Md. XX] formatında belirt.\n" +
        "- Gerekirse \"Bu durumda şunları yapabilirsin:\" şeklinde liste ver.\n" +
        "- Emsal Yargıtay kararı bulursan yanıtın sonunda şu formatta listele:\n" +
        "  EMSAL_KARARLAR_BASLANGIC\n" +
        "  KARAR: Yargıtay 9. HD, 2023/1234 E. 2023/5678 K.\n" +
        "  OZET: İşçinin kıdem tazminatı hakkına ilişkin karar özeti.\n" +
        "  URL: https://karararama.yargitay.gov.tr/...\n" +
        "  EMSAL_KARARLAR_BITIS\n" +
        "- Karar bulamazsan EMSAL_KARARLAR bloğunu ekleme.\n" +
        "- Yanıtın sonuna her zaman şu notu ekle: \"⚠️ Bu bilgi genel nitelikte olup hukuki tavsiye yerine geçmez.\"\n\n" +
        "SINIRLAR:\n" +
        "- Kanun metninde karşılığı olmayan konularda yorum yapma, \"Bu konuda kesin bilgi veremem\" de.\n" +
        "- Başka hukuk alanlarına (ceza, medeni vb.) girme.\n" +
        "- Hiçbir zaman belirli bir avukat veya firma tavsiye etme.\n" +
        "- Kullanıcıyı duygusal olarak manipüle etme.";

    public ClaudeService(IConfiguration configuration, KanunService kanunService)
    {
        var apiKey = configuration["Anthropic:ApiKey"];
        _client = string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_API_KEY_HERE"
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = apiKey };
        _kanunService = kanunService;
    }

    public async Task<(string Reply, List<SourceItem> Sources, bool IsError)> GetResponseAsync(string userMessage)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            var systemBlocks = BuildSystemBlocks();

            var messages = new List<MessageParam>
            {
                new MessageParam { Role = Role.User, Content = userMessage }
            };

            var tools = new List<ToolUnion>
            {
                new WebSearchTool20250305 { Name = JsonSerializer.SerializeToElement("web_search") }
            };

            string rawReply = string.Empty;

            for (int i = 0; i < 5; i++)
            {
                var response = await _client.Messages.Create(new MessageCreateParams
                {
                    Model = "claude-opus-4-7",
                    MaxTokens = 4096,
                    System = systemBlocks,
                    Tools = tools,
                    Messages = messages
                }, cts.Token);

                rawReply = string.Join("", response.Content
                    .Select(b => b.Value)
                    .OfType<TextBlock>()
                    .Select(t => t.Text));

                if (response.StopReason != StopReason.ToolUse)
                    break;

                var rawBlocks = response.Content
                    .Select(b => b.Value)
                    .Select(BuildRawBlock)
                    .Where(e => e.HasValue)
                    .Select(e => e!.Value)
                    .ToArray();

                if (rawBlocks.Length == 0) break;

                messages.Add(MessageParam.FromRawUnchecked(new Dictionary<string, JsonElement>
                {
                    ["role"] = JsonSerializer.SerializeToElement("assistant"),
                    ["content"] = JsonSerializer.SerializeToElement(rawBlocks)
                }));

                var clientToolUseBlocks = response.Content
                    .Select(b => b.Value)
                    .OfType<ToolUseBlock>()
                    .ToList();

                if (clientToolUseBlocks.Any())
                {
                    var toolResults = clientToolUseBlocks
                        .Select(t => JsonSerializer.SerializeToElement(new
                        {
                            type = "tool_result",
                            tool_use_id = t.ID,
                            content = ""
                        }))
                        .ToArray();

                    messages.Add(MessageParam.FromRawUnchecked(new Dictionary<string, JsonElement>
                    {
                        ["role"] = JsonSerializer.SerializeToElement("user"),
                        ["content"] = JsonSerializer.SerializeToElement(toolResults)
                    }));
                }
            }

            var (cleanReply, sources) = ParseSources(rawReply);
            return (cleanReply, sources, false);
        }
        catch (OperationCanceledException)
        {
            return ("İstek zaman aşımına uğradı, lütfen tekrar deneyin.", new List<SourceItem>(), true);
        }
        catch (AnthropicUnauthorizedException)
        {
            return ("API anahtarı geçersiz.", new List<SourceItem>(), true);
        }
        catch (Exception ex) when (ex.Message.Contains("529") || ex.Message.Contains("overloaded", StringComparison.OrdinalIgnoreCase))
        {
            return ("Şu an yoğunluk var, lütfen birkaç saniye sonra tekrar deneyin.", new List<SourceItem>(), true);
        }
        catch (HttpRequestException)
        {
            return ("Bağlantı kurulamadı, internet bağlantınızı kontrol edin.", new List<SourceItem>(), true);
        }
        catch (Exception ex) when (ex.Message.Contains("credit balance"))
        {
            return ("Hizmet şu an kullanılamıyor, lütfen daha sonra tekrar deneyin.", new List<SourceItem>(), true);
        }
        catch
        {
            return ("Sunucu hatası oluştu, lütfen tekrar deneyin.", new List<SourceItem>(), true);
        }
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

        // Her KARAR/OZET/URL üçlüsünü parse et
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

    private static JsonElement? BuildRawBlock(object block) =>
        block switch
        {
            TextBlock tb => JsonSerializer.SerializeToElement(new
            {
                type = "text",
                text = tb.Text
            }),
            ToolUseBlock tub => JsonSerializer.SerializeToElement(new
            {
                type = "tool_use",
                id   = tub.ID,
                name = tub.Name,
                input = tub.Input
            }),
            ServerToolUseBlock stub => JsonSerializer.SerializeToElement(new
            {
                type  = "server_tool_use",
                id    = stub.ID,
                name  = stub.Name,
                input = stub.Input
            }),
            _ => (JsonElement?)null
        };

    private List<TextBlockParam> BuildSystemBlocks()
    {
        if (!_kanunService.HasKanunMetni())
            return new List<TextBlockParam> { new TextBlockParam { Text = SystemInstructions } };

        return new List<TextBlockParam>
        {
            new TextBlockParam { Text = SystemInstructions },
            new TextBlockParam
            {
                Text = "KANUN METNİ BAĞLAMI:\n\n" + _kanunService.GetKanunMetni(),
                CacheControl = new CacheControlEphemeral()
            }
        };
    }
}

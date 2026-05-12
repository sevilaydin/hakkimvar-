namespace Hakkimvar.Models;

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public List<SourceItem> Sources { get; set; } = new();
}

public class SourceItem
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

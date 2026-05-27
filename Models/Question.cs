namespace Hakkimvar.Models;

public class Question
{
    public int      Id          { get; set; }
    public string   Text        { get; set; } = string.Empty;
    public string   Category    { get; set; } = string.Empty;
    public string   Answer      { get; set; } = string.Empty;
    public int      SourceCount { get; set; }
    public int      ViewCount   { get; set; } = 1;
    public int?     Helpful     { get; set; }   // 1-5 yıldız, null = oy verilmedi
    public int      ResponseMs  { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
}

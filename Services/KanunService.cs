using System.Text;

namespace Hakkimvar.Services;

public class KanunService
{
    private readonly string _kanunMetni;

    public KanunService(IWebHostEnvironment env)
    {
        var filePath = Path.Combine(env.ContentRootPath, "Data", "is_kanunu.txt");
        _kanunMetni = File.Exists(filePath)
            ? File.ReadAllText(filePath, Encoding.UTF8)
            : string.Empty;
    }

    public string GetKanunMetni() => _kanunMetni;
    public bool HasKanunMetni() => !string.IsNullOrWhiteSpace(_kanunMetni);
}

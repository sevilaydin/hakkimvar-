using System.Collections.Concurrent;

namespace Hakkimvar.Services;

public record QuestionLog(
    DateTime AskedAt,
    string   Category,
    string   Preview,   // ilk 120 karakter
    bool     IsError,
    int      ResponseMs
);

/// <summary>
/// Hafıza içi istatistik + son sorular.
/// Render restart olunca sıfırlanır — kalıcı DB eklenene kadar yeterli.
/// </summary>
public class AnalyticsService
{
    private const int MaxLog = 100;

    private int _totalQuestions;
    private int _totalErrors;

    private readonly ConcurrentDictionary<string, int> _categoryCount = new();
    private readonly ConcurrentDictionary<int, int>    _hourlyCount   = new();
    private readonly ConcurrentQueue<QuestionLog>      _recentLog     = new();

    public void TrackQuestion(string category, string message, int responseMs)
    {
        Interlocked.Increment(ref _totalQuestions);
        _categoryCount.AddOrUpdate(category, 1, (_, v) => v + 1);
        _hourlyCount.AddOrUpdate(DateTime.UtcNow.Hour, 1, (_, v) => v + 1);

        var preview = message.Length > 120 ? message[..120] + "…" : message;
        _recentLog.Enqueue(new QuestionLog(DateTime.UtcNow, category, preview, false, responseMs));

        // Kuyruğu 100 ile sınırla
        while (_recentLog.Count > MaxLog)
            _recentLog.TryDequeue(out _);
    }

    public void TrackError(string category, string message, int responseMs)
    {
        Interlocked.Increment(ref _totalErrors);

        var preview = message.Length > 120 ? message[..120] + "…" : message;
        _recentLog.Enqueue(new QuestionLog(DateTime.UtcNow, category, preview, true, responseMs));

        while (_recentLog.Count > MaxLog)
            _recentLog.TryDequeue(out _);
    }

    public object GetStats() => new
    {
        total_questions  = _totalQuestions,
        total_errors     = _totalErrors,
        by_category      = _categoryCount.OrderByDescending(x => x.Value)
                                          .ToDictionary(x => x.Key, x => x.Value),
        busiest_hour_utc = _hourlyCount.OrderByDescending(x => x.Value).FirstOrDefault().Key,
        recent_questions = _recentLog.Reverse()   // en yeni önce
                                     .Select(q => new
                                     {
                                         asked_at    = q.AskedAt.ToString("dd.MM HH:mm"),
                                         category    = q.Category,
                                         preview     = q.Preview,
                                         is_error    = q.IsError,
                                         response_ms = q.ResponseMs
                                     })
                                     .ToList(),
        as_of_utc = DateTime.UtcNow
    };
}

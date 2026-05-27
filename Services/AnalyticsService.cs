using System.Collections.Concurrent;

namespace Hakkimvar.Services;

/// <summary>
/// Hafıza içi basit istatistik tutucusu.
/// Render restart olunca sıfırlanır — kalıcı DB eklenene kadar yeterli.
/// </summary>
public class AnalyticsService
{
    private int _totalQuestions;
    private int _totalErrors;
    private readonly ConcurrentDictionary<string, int> _categoryCount = new();
    private readonly ConcurrentDictionary<int, int> _hourlyCount = new(); // saat → sayı

    public void TrackQuestion(string category)
    {
        Interlocked.Increment(ref _totalQuestions);
        _categoryCount.AddOrUpdate(category, 1, (_, v) => v + 1);
        var hour = DateTime.UtcNow.Hour;
        _hourlyCount.AddOrUpdate(hour, 1, (_, v) => v + 1);
    }

    public void TrackError() => Interlocked.Increment(ref _totalErrors);

    public object GetStats() => new
    {
        total_questions  = _totalQuestions,
        total_errors     = _totalErrors,
        by_category      = _categoryCount.OrderByDescending(x => x.Value)
                                          .ToDictionary(x => x.Key, x => x.Value),
        busiest_hour_utc = _hourlyCount.OrderByDescending(x => x.Value).FirstOrDefault().Key,
        as_of_utc        = DateTime.UtcNow
    };
}

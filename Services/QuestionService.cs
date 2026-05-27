using Hakkimvar.Data;
using Hakkimvar.Models;
using Microsoft.EntityFrameworkCore;

namespace Hakkimvar.Services;

public interface IQuestionService
{
    Task SaveAsync(string text, string category, string answer, int sourceCount, int responseMs);
    Task<List<Question>> GetRecentAsync(int count = 50);
    Task<List<Question>> GetByCategoryAsync(string category, int count = 20);
    Task<List<(string Category, int Count)>> GetCategoryStatsAsync(int days = 30);
    Task<bool> RateAsync(int id, int stars);
}

public class QuestionService(AppDbContext db, ILogger<QuestionService> logger) : IQuestionService
{
    public async Task SaveAsync(string text, string category, string answer, int sourceCount, int responseMs)
    {
        try
        {
            db.Questions.Add(new Question
            {
                Text        = text.Length > 2000 ? text[..2000] : text,
                Category    = category,
                Answer      = answer.Length > 8000 ? answer[..8000] : answer,
                SourceCount = sourceCount,
                ResponseMs  = responseMs,
                CreatedAt   = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Kaydetme hatası chat akışını durdurmasın
            logger.LogError(ex, "Soru kaydedilemedi");
        }
    }

    public Task<List<Question>> GetRecentAsync(int count = 50) =>
        db.Questions.OrderByDescending(q => q.CreatedAt).Take(count).ToListAsync();

    public Task<List<Question>> GetByCategoryAsync(string category, int count = 20) =>
        db.Questions.Where(q => q.Category == category)
                    .OrderByDescending(q => q.CreatedAt)
                    .Take(count)
                    .ToListAsync();

    public async Task<List<(string Category, int Count)>> GetCategoryStatsAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await db.Questions
            .Where(q => q.CreatedAt >= since)
            .GroupBy(q => q.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => new ValueTuple<string, int>(x.Key, x.Count))
            .ToListAsync();
    }

    public async Task<bool> RateAsync(int id, int stars)
    {
        var q = await db.Questions.FindAsync(id);
        if (q == null) return false;
        q.Helpful = Math.Clamp(stars, 1, 5);
        await db.SaveChangesAsync();
        return true;
    }
}

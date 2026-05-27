using Hakkimvar.Models;
using Microsoft.EntityFrameworkCore;

namespace Hakkimvar.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Question>             Questions             { get; set; }
    public DbSet<Subscription>         Subscriptions         { get; set; }
    public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Question>(e =>
        {
            e.HasIndex(q => q.Category);
            e.HasIndex(q => q.CreatedAt);
            e.Property(q => q.Text).HasMaxLength(2000);
        });

        b.Entity<Subscription>(e =>
        {
            e.HasIndex(s => s.Email).IsUnique();
            e.Property(s => s.Email).HasMaxLength(256);
        });

        b.Entity<NewsletterSubscriber>(e =>
        {
            e.HasIndex(n => n.Email).IsUnique();
            e.HasIndex(n => n.UnsubscribeToken);
            e.Property(n => n.Email).HasMaxLength(256);
        });
    }
}

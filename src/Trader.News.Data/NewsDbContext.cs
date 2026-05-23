using Microsoft.EntityFrameworkCore;
using Trader.News.Data.Entities;
using Trader.News.Data.Enums;

namespace Trader.News.Data;

/// <summary>
/// EF Core DbContext for the News subsystem.
/// Uses a dedicated PostgreSQL database (<c>trader_news</c>).
/// </summary>
public class NewsDbContext : DbContext
{
    public NewsDbContext(DbContextOptions<NewsDbContext> options)
        : base(options) { }

    public DbSet<NewsSource> NewsSources => Set<NewsSource>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // ── NewsSource ────────────────────────────────────────────────────
        model.Entity<NewsSource>(e =>
        {
            e.ToTable("news_sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Uri).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Username).HasMaxLength(200);
            e.Property(x => x.PasswordEncrypted).HasMaxLength(1000);
            e.Property(x => x.Category)
             .HasConversion<int>()
             .IsRequired();
            e.HasIndex(x => x.IsEnabled);
        });

        // ── NewsItem ──────────────────────────────────────────────────────
        model.Entity<NewsItem>(e =>
        {
            e.ToTable("news_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Uri).HasMaxLength(2000);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(NewsItem.SummaryMaxLength);
            e.Property(x => x.Classification)
             .HasConversion<int>()
             .IsRequired();
            e.Property(x => x.ValuationScore).HasPrecision(5, 4);

            e.HasIndex(x => x.SourceId);
            e.HasIndex(x => x.NewsDate);
            e.HasIndex(x => x.Classification);
            // Avoid duplicate items from the same source
            e.HasIndex(x => new { x.SourceId, x.Uri }).IsUnique().HasFilter("uri IS NOT NULL");

            e.HasOne(x => x.Source)
             .WithMany(s => s.NewsItems)
             .HasForeignKey(x => x.SourceId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

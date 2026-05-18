using Microsoft.EntityFrameworkCore;
using Trader.MarketData.Data.Entities;

namespace Trader.MarketData.Data;

/// <summary>
/// EF Core DbContext for the MarketData subsystem.
///
/// Applies TimescaleDB hypertable creation via raw SQL in migrations:
///   SELECT create_hypertable('quote_daily',    'date',       if_not_exists => TRUE);
///   SELECT create_hypertable('quote_intraday', 'timestamp',  if_not_exists => TRUE);
/// </summary>
public class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options)
        : base(options) { }

    public DbSet<InstrumentType> InstrumentTypes => Set<InstrumentType>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<QuoteDaily> QuoteDaily => Set<QuoteDaily>();
    public DbSet<QuoteIntraday> QuoteIntraday => Set<QuoteIntraday>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // ── InstrumentType ────────────────────────────────────────────────
        model.Entity<InstrumentType>(e =>
        {
            e.ToTable("instrument_types");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        // ── Instrument ────────────────────────────────────────────────────
        model.Entity<Instrument>(e =>
        {
            e.ToTable("instruments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Ticker).HasMaxLength(50).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.Market).HasMaxLength(50);
            e.Property(x => x.ProviderId).HasMaxLength(100).IsRequired();
            // Ticker is unique per market
            e.HasIndex(x => new { x.Ticker, x.Market }).IsUnique();
            e.HasOne(x => x.InstrumentType)
             .WithMany(t => t.Instruments)
             .HasForeignKey(x => x.InstrumentTypeId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── QuoteDaily ────────────────────────────────────────────────────
        model.Entity<QuoteDaily>(e =>
        {
            e.ToTable("quote_daily");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Settlement).HasMaxLength(10);
            e.Property(x => x.ChangePercent).HasMaxLength(20);
            // All price columns stored with 18,6 precision
            e.Property(x => x.Open).HasPrecision(18, 6);
            e.Property(x => x.High).HasPrecision(18, 6);
            e.Property(x => x.Low).HasPrecision(18, 6);
            e.Property(x => x.Close).HasPrecision(18, 6);
            e.Property(x => x.Volume).HasPrecision(28, 6);
            e.Property(x => x.PreviousClose).HasPrecision(18, 6);
            e.Property(x => x.Change).HasPrecision(18, 6);
            // One row per instrument/date/settlement
            e.HasIndex(x => new { x.InstrumentId, x.Date, x.Settlement }).IsUnique();
            // TimescaleDB index for time-range queries
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.Instrument)
             .WithMany(i => i.DailyQuotes)
             .HasForeignKey(x => x.InstrumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── QuoteIntraday ─────────────────────────────────────────────────
        model.Entity<QuoteIntraday>(e =>
        {
            e.ToTable("quote_intraday");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ChangePercent).HasMaxLength(20);
            e.Property(x => x.Price).HasPrecision(18, 6);
            e.Property(x => x.Volume).HasPrecision(28, 6);
            e.Property(x => x.Open).HasPrecision(18, 6);
            e.Property(x => x.High).HasPrecision(18, 6);
            e.Property(x => x.Low).HasPrecision(18, 6);
            e.Property(x => x.Change).HasPrecision(18, 6);
            // One tick per instrument per moment
            e.HasIndex(x => new { x.InstrumentId, x.Timestamp }).IsUnique();
            // TimescaleDB index for time-range queries
            e.HasIndex(x => x.Timestamp);
            e.HasOne(x => x.Instrument)
             .WithMany(i => i.IntradayQuotes)
             .HasForeignKey(x => x.InstrumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

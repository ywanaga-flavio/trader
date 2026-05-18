using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trader.Core.Providers;
using Trader.MarketData.Data;
using Trader.MarketData.Data.Entities;
using Trader.MarketData.Worker.Configuration;

namespace Trader.MarketData.Worker.Workers;

/// <summary>
/// On startup, discovers all active instruments via the provider's
/// <c>SearchInstrumentsAsync</c> and back-fills daily OHLCV bars from
/// <see cref="HistoricalConfig.FromDate"/> to today.
///
/// Existing rows are skipped (upsert-skip semantics).
/// </summary>
public class HistoricalQuoteWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQuoteProvider _quoteProvider;
    private readonly MarketDataWorkerOptions _options;
    private readonly ILogger<HistoricalQuoteWorker> _logger;

    public HistoricalQuoteWorker(
        IServiceScopeFactory scopeFactory,
        IQuoteProvider quoteProvider,
        IOptions<MarketDataWorkerOptions> options,
        ILogger<HistoricalQuoteWorker> logger)
    {
        _scopeFactory  = scopeFactory;
        _quoteProvider = quoteProvider;
        _options       = options.Value;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var providerCfg in _options.Providers)
        {
            if (!providerCfg.Enabled || !providerCfg.Historical.Enabled) continue;

            _logger.LogInformation(
                "[HistoricalWorker] Starting back-fill for provider {ProviderId}", providerCfg.ProviderId);

            await RunBackFillAsync(providerCfg, stoppingToken);
        }

        _logger.LogInformation("[HistoricalWorker] Back-fill complete.");
    }

    private async Task RunBackFillAsync(ProviderConfig cfg, CancellationToken ct)
    {
        // Discover instruments for every configured market.
        var instruments = new List<Core.Models.Instrument>();
        foreach (var market in cfg.Markets)
        {
            try
            {
                var found = await _quoteProvider.SearchInstrumentsAsync(
                    market: market, ct: ct);
                instruments.AddRange(found);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[HistoricalWorker] SearchInstruments failed for market {Market}", market);
            }
        }

        _logger.LogInformation(
            "[HistoricalWorker] Discovered {Count} instruments", instruments.Count);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = cfg.Historical.ThreadCount,
            CancellationToken      = ct
        };

        await Parallel.ForEachAsync(instruments, parallelOptions, async (inst, token) =>
        {
            await FetchAndPersistDailyAsync(inst, cfg, token);
        });
    }

    private async Task FetchAndPersistDailyAsync(
        Core.Models.Instrument inst, ProviderConfig cfg, CancellationToken ct)
    {
        try
        {
            var from = new DateTimeOffset(
                cfg.Historical.FromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var to = new DateTimeOffset(
                DateOnly.FromDateTime(DateTime.UtcNow).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

            var bars = await _quoteProvider.GetHistoricalAsync(
                inst.Ticker, from, to, Core.Models.BarInterval.Day1, ct);

            if (bars.Count == 0) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();

            var dbInstrument = await GetOrCreateInstrumentAsync(db, inst, cfg.ProviderId, ct);

            foreach (var bar in bars)
            {
                var date = DateOnly.FromDateTime(bar.Timestamp.DateTime);

                var exists = await db.QuoteDaily.AnyAsync(
                    q => q.InstrumentId == dbInstrument.Id && q.Date == date, ct);

                if (exists) continue;

                db.QuoteDaily.Add(new QuoteDaily
                {
                    InstrumentId = dbInstrument.Id,
                    Date         = date,
                    Open         = bar.Open,
                    High         = bar.High,
                    Low          = bar.Low,
                    Close        = bar.Close,
                    Volume       = bar.Volume,
                    Settlement   = bar.Settlement,
                    ProviderId   = cfg.ProviderId
                });
            }

            await db.SaveChangesAsync(ct);

            _logger.LogDebug(
                "[HistoricalWorker] Saved bars for {Ticker} ({Count} bars)", inst.Ticker, bars.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[HistoricalWorker] Failed to fetch history for {Ticker}", inst.Ticker);
        }
    }

    private static async Task<Instrument> GetOrCreateInstrumentAsync(
        MarketDataDbContext db,
        Core.Models.Instrument source,
        string providerId,
        CancellationToken ct)
    {
        var existing = await db.Instruments.FirstOrDefaultAsync(
            i => i.Ticker == source.Ticker, ct);

        if (existing is not null) return existing;

        var entity = new Instrument
        {
            Ticker         = source.Ticker,
            Description    = source.Description,
            Currency       = source.Currency,
            Market         = source.Market,
            ProviderId     = providerId,
            IsActive       = true,
            DiscoveredAt   = DateTimeOffset.UtcNow
        };

        db.Instruments.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }
}

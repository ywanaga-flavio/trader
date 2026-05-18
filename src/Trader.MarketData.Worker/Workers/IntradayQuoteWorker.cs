using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trader.Core.Providers;
using Trader.MarketData.Data;
using Trader.MarketData.Data.Entities;
using Trader.MarketData.Worker.Configuration;

namespace Trader.MarketData.Worker.Workers;

/// <summary>
/// Polls the configured provider(s) for live quotes during market hours and
/// persists each tick to <c>quote_intraday</c>.
///
/// The worker sleeps <see cref="IntradayConfig.PollIntervalSeconds"/> between
/// rounds. When no market is open it sleeps until the next open window.
/// </summary>
public class IntradayQuoteWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQuoteProvider _quoteProvider;
    private readonly MarketDataWorkerOptions _options;
    private readonly ILogger<IntradayQuoteWorker> _logger;

    public IntradayQuoteWorker(
        IServiceScopeFactory scopeFactory,
        IQuoteProvider quoteProvider,
        IOptions<MarketDataWorkerOptions> options,
        ILogger<IntradayQuoteWorker> logger)
    {
        _scopeFactory  = scopeFactory;
        _quoteProvider = quoteProvider;
        _options       = options.Value;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var providerCfg in _options.Providers)
            {
                if (!providerCfg.Enabled || !providerCfg.Intraday.Enabled) continue;

                var openMarkets = providerCfg.MarketHours
                    .Where(mh => providerCfg.Markets.Contains(mh.Market) && mh.IsOpen(now))
                    .ToList();

                if (openMarkets.Count == 0) continue;

                await PollProviderAsync(providerCfg, openMarkets.Select(m => m.Market).ToList(), stoppingToken);
            }

            // Use the shortest configured interval across enabled providers.
            var intervalSeconds = _options.Providers
                .Where(p => p.Enabled && p.Intraday.Enabled)
                .Select(p => p.Intraday.PollIntervalSeconds)
                .DefaultIfEmpty(30)
                .Min();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollProviderAsync(
        ProviderConfig cfg, List<string> openMarkets, CancellationToken ct)
    {
        // Collect active instruments for the open markets from DB.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();

        var instruments = await db.Instruments
            .Where(i => i.IsActive && i.ProviderId == cfg.ProviderId
                        && openMarkets.Contains(i.Market!))
            .AsNoTracking()
            .ToListAsync(ct);

        if (instruments.Count == 0) return;

        _logger.LogDebug(
            "[IntradayWorker] Polling {Count} instruments for {ProviderId}",
            instruments.Count, cfg.ProviderId);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = cfg.Intraday.ThreadCount,
            CancellationToken      = ct
        };

        await Parallel.ForEachAsync(instruments, parallelOptions, async (inst, token) =>
        {
            await FetchAndPersistTickAsync(db, inst, cfg.ProviderId, token);
        });
    }

    private async Task FetchAndPersistTickAsync(
        MarketDataDbContext db,
        Instrument inst,
        string providerId,
        CancellationToken ct)
    {
        try
        {
            var quote = await _quoteProvider.GetLastQuoteAsync(inst.Ticker, ct);

            var alreadyExists = await db.QuoteIntraday.AnyAsync(
                q => q.InstrumentId == inst.Id && q.Timestamp == quote.Timestamp, ct);

            if (alreadyExists) return;

            db.QuoteIntraday.Add(new QuoteIntraday
            {
                InstrumentId  = inst.Id,
                Timestamp     = quote.Timestamp,
                Price         = quote.Price,
                Volume        = quote.Volume,
                Open          = quote.OpeningPrice,
                High          = quote.High,
                Low           = quote.Low,
                Change        = quote.Change,
                ChangePercent = quote.ChangePercent,
                ProviderId    = providerId
            });

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[IntradayWorker] Failed to poll {Ticker}", inst.Ticker);
        }
    }
}

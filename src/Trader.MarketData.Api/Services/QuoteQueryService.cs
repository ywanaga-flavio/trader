using Microsoft.EntityFrameworkCore;
using Trader.Core.Providers;
using Trader.MarketData.Data;
using Trader.MarketData.Data.Entities;
using Trader.MarketData.Api.Models;

namespace Trader.MarketData.Api.Services;

/// <summary>
/// Core query logic shared by both the REST controllers and the gRPC service.
///
/// Strategy per query:
///   - <c>online=false</c> (default): query DB only.
///   - <c>online=true</c>: try provider first; on failure fall back to DB
///     and set <see cref="DataSource.DatabaseFallback"/> in the response.
///   - If data returned from the provider does not exist in DB, persist it.
/// </summary>
public class QuoteQueryService
{
    private readonly MarketDataDbContext _db;
    private readonly IQuoteProvider _quoteProvider;
    private readonly ILogger<QuoteQueryService> _logger;

    public QuoteQueryService(
        MarketDataDbContext db,
        IQuoteProvider quoteProvider,
        ILogger<QuoteQueryService> logger)
    {
        _db = db;
        _quoteProvider = quoteProvider;
        _logger = logger;
    }

    // ─── GetLastQuote ─────────────────────────────────────────────────────────

    public async Task<QuoteResponse?> GetLastQuoteAsync(
        string symbol, bool online, CancellationToken ct)
    {
        if (online)
        {
            try
            {
                var q = await _quoteProvider.GetLastQuoteAsync(symbol, ct);
                await PersistIntradayAsync(symbol, q, ct);
                return MapOnlineQuote(q);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Online GetLastQuote failed for {Symbol}, falling back to DB", symbol);
                var fallback = await GetLastQuoteFromDbAsync(symbol, ct);
                return fallback is null ? null : fallback with
                {
                    DataSource = DataSource.DatabaseFallback,
                    FallbackReason = ex.Message
                };
            }
        }

        return await GetLastQuoteFromDbAsync(symbol, ct);
    }

    // ─── GetDailyQuotes ───────────────────────────────────────────────────────

    public async Task<DailyQuotesResponse> GetDailyQuotesAsync(
        string symbol, DateOnly from, DateOnly to, bool online, CancellationToken ct)
    {
        if (online)
        {
            try
            {
                var bars = await _quoteProvider.GetHistoricalAsync(
                    symbol,
                    new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero),
                    Core.Models.BarInterval.Day1, ct);

                await PersistDailyBarsAsync(symbol, bars, ct);
                return new DailyQuotesResponse
                {
                    Quotes = bars.Select(b => MapBar(symbol, b)).ToList(),
                    DataSource = DataSource.Online
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Online GetHistorical failed for {Symbol}, falling back to DB", symbol);
                var fallback = await GetDailyQuotesFromDbAsync(symbol, from, to, ct);
                return new DailyQuotesResponse
                {
                    Quotes = fallback,
                    DataSource = DataSource.DatabaseFallback,
                    FallbackReason = ex.Message
                };
            }
        }

        return new DailyQuotesResponse
        {
            Quotes = await GetDailyQuotesFromDbAsync(symbol, from, to, ct),
            DataSource = DataSource.Database
        };
    }

    // ─── GetIntradayQuotes ────────────────────────────────────────────────────

    public async Task<IntradayQuotesResponse> GetIntradayQuotesAsync(
        string symbol, DateOnly date, bool online, CancellationToken ct)
    {
        if (online)
        {
            // For intraday we only have live data — provider does not support
            // historical ticks, so we always return from DB for past dates.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (date == today)
            {
                try
                {
                    var q = await _quoteProvider.GetLastQuoteAsync(symbol, ct);
                    await PersistIntradayAsync(symbol, q, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Online intraday fetch failed for {Symbol} on {Date}", symbol, date);
                }
            }
        }

        var ticks = await GetIntradayFromDbAsync(symbol, date, ct);
        return new IntradayQuotesResponse
        {
            Ticks = ticks,
            DataSource = DataSource.Database
        };
    }

    // ─── GetQuotesByType ──────────────────────────────────────────────────────

    public async Task<DailyQuotesResponse> GetQuotesByTypeAsync(
        string instrumentTypeCode, DateOnly date, CancellationToken ct)
    {
        var quotes = await _db.QuoteDaily
            .Include(q => q.Instrument)
                .ThenInclude(i => i.InstrumentType)
            .Where(q =>
                q.Date == date &&
                q.Instrument.InstrumentType != null &&
                q.Instrument.InstrumentType.Code == instrumentTypeCode)
            .AsNoTracking()
            .ToListAsync(ct);

        return new DailyQuotesResponse
        {
            Quotes = quotes.Select(q => MapDbDaily(q)).ToList(),
            DataSource = DataSource.Database
        };
    }

    // ─── SearchInstruments ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<InstrumentResponse>> SearchInstrumentsAsync(
        string? query, string? market, string? instrumentType, CancellationToken ct)
    {
        var q = _db.Instruments
            .Include(i => i.InstrumentType)
            .Where(i => i.IsActive)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(i =>
                EF.Functions.ILike(i.Ticker, $"%{query}%") ||
                (i.Description != null && EF.Functions.ILike(i.Description, $"%{query}%")));

        if (!string.IsNullOrWhiteSpace(market))
            q = q.Where(i => i.Market == market);

        if (!string.IsNullOrWhiteSpace(instrumentType))
            q = q.Where(i => i.InstrumentType != null && i.InstrumentType.Code == instrumentType);

        var instruments = await q.ToListAsync(ct);
        return instruments.Select(MapInstrument).ToList();
    }

    // ─── Private DB helpers ───────────────────────────────────────────────────

    private async Task<QuoteResponse?> GetLastQuoteFromDbAsync(string symbol, CancellationToken ct)
    {
        var row = await _db.QuoteIntraday
            .Include(q => q.Instrument)
            .Where(q => q.Instrument.Ticker == symbol)
            .OrderByDescending(q => q.Timestamp)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new QuoteResponse
        {
            Symbol = symbol,
            Price = row.Price,
            Volume = row.Volume,
            Open = row.Open ?? 0,
            High = row.High ?? 0,
            Low = row.Low ?? 0,
            Change = row.Change,
            ChangePercent = row.ChangePercent,
            Timestamp = row.Timestamp,
            ProviderId = row.ProviderId,
            DataSource = DataSource.Database
        };
    }

    private async Task<IReadOnlyList<DailyQuoteResponse>> GetDailyQuotesFromDbAsync(
        string symbol, DateOnly from, DateOnly to, CancellationToken ct)
    {
        return await _db.QuoteDaily
            .Include(q => q.Instrument)
            .Where(q => q.Instrument.Ticker == symbol && q.Date >= from && q.Date <= to)
            .OrderBy(q => q.Date)
            .AsNoTracking()
            .Select(q => MapDbDaily(q))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<IntradayTickResponse>> GetIntradayFromDbAsync(
        string symbol, DateOnly date, CancellationToken ct)
    {
        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        return await _db.QuoteIntraday
            .Include(q => q.Instrument)
            .Where(q =>
                q.Instrument.Ticker == symbol &&
                q.Timestamp >= dayStart &&
                q.Timestamp < dayEnd)
            .OrderBy(q => q.Timestamp)
            .AsNoTracking()
            .Select(q => new IntradayTickResponse
            {
                Symbol = symbol,
                Timestamp = q.Timestamp,
                Price = q.Price,
                Volume = q.Volume,
                Open = q.Open,
                High = q.High,
                Low = q.Low,
                Change = q.Change,
                ChangePercent = q.ChangePercent,
                ProviderId = q.ProviderId
            })
            .ToListAsync(ct);
    }

    // ─── Persistence helpers ──────────────────────────────────────────────────

    private async Task PersistIntradayAsync(
        string symbol, Core.Models.Quote q, CancellationToken ct)
    {
        var instrument = await GetOrCreateInstrumentAsync(symbol, q.Provider, ct);

        var existing = await _db.QuoteIntraday
            .FirstOrDefaultAsync(x =>
                x.InstrumentId == instrument.Id && x.Timestamp == q.Timestamp, ct);

        if (existing is not null) return;

        _db.QuoteIntraday.Add(new QuoteIntraday
        {
            InstrumentId = instrument.Id,
            Timestamp = q.Timestamp,
            Price = q.Price,
            Volume = q.Volume,
            Open = q.OpeningPrice,
            High = q.High,
            Low = q.Low,
            Change = q.Change,
            ChangePercent = q.ChangePercent,
            ProviderId = q.Provider ?? "unknown"
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task PersistDailyBarsAsync(
        string symbol, IReadOnlyList<Core.Models.Bar> bars, CancellationToken ct)
    {
        if (bars.Count == 0) return;
        var instrument = await GetOrCreateInstrumentAsync(symbol, "unknown", ct);

        foreach (var bar in bars)
        {
            var date = DateOnly.FromDateTime(bar.Timestamp.DateTime);
            var exists = await _db.QuoteDaily
                .AnyAsync(x => x.InstrumentId == instrument.Id && x.Date == date, ct);
            if (exists) continue;

            _db.QuoteDaily.Add(new QuoteDaily
            {
                InstrumentId = instrument.Id,
                Date = date,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume,
                Settlement = bar.Settlement,
                ProviderId = "unknown"
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Instrument> GetOrCreateInstrumentAsync(
        string ticker, string? providerId, CancellationToken ct)
    {
        var instrument = await _db.Instruments
            .FirstOrDefaultAsync(i => i.Ticker == ticker, ct);

        if (instrument is not null) return instrument;

        instrument = new Instrument
        {
            Ticker = ticker,
            ProviderId = providerId ?? "unknown"
        };
        _db.Instruments.Add(instrument);
        await _db.SaveChangesAsync(ct);
        return instrument;
    }

    // ─── Mappings ─────────────────────────────────────────────────────────────

    private static QuoteResponse MapOnlineQuote(Core.Models.Quote q) => new()
    {
        Symbol = q.Symbol,
        Price = q.Price,
        Volume = q.Volume,
        Open = q.OpeningPrice,
        High = q.High,
        Low = q.Low,
        PreviousClose = q.PreviousClose,
        Change = q.Change,
        ChangePercent = q.ChangePercent,
        Timestamp = q.Timestamp,
        ProviderId = q.Provider,
        Settlement = q.Settlement,
        DataSource = DataSource.Online
    };

    private static DailyQuoteResponse MapBar(string symbol, Core.Models.Bar b) => new()
    {
        Symbol = symbol,
        Date = DateOnly.FromDateTime(b.Timestamp.DateTime),
        Open = b.Open,
        High = b.High,
        Low = b.Low,
        Close = b.Close,
        Volume = b.Volume,
        Settlement = b.Settlement,
        ProviderId = "unknown"
    };

    private static DailyQuoteResponse MapDbDaily(QuoteDaily q) => new()
    {
        Symbol = q.Instrument?.Ticker ?? string.Empty,
        Date = q.Date,
        Open = q.Open,
        High = q.High,
        Low = q.Low,
        Close = q.Close,
        Volume = q.Volume,
        PreviousClose = q.PreviousClose,
        Change = q.Change,
        ChangePercent = q.ChangePercent,
        Settlement = q.Settlement,
        ProviderId = q.ProviderId
    };

    private static InstrumentResponse MapInstrument(Instrument i) => new()
    {
        Id = i.Id,
        Ticker = i.Ticker,
        Description = i.Description,
        Currency = i.Currency,
        Market = i.Market,
        InstrumentType = i.InstrumentType?.Code,
        ProviderId = i.ProviderId
    };
}

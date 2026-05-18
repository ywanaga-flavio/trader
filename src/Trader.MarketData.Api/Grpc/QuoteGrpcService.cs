using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Trader.MarketData.Api.Services;

namespace Trader.MarketData.Api.Grpc;

/// <summary>
/// gRPC implementation of <see cref="QuoteService"/>.
///
/// Delegates all query logic to <see cref="QuoteQueryService"/> and converts
/// domain responses to protobuf messages.
/// </summary>
[Authorize]
public class QuoteGrpcService : QuoteService.QuoteServiceBase
{
    private readonly QuoteQueryService _svc;
    private readonly ILogger<QuoteGrpcService> _logger;

    public QuoteGrpcService(QuoteQueryService svc, ILogger<QuoteGrpcService> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    public override async Task<QuoteReply> GetLastQuote(
        GetLastQuoteRequest request, ServerCallContext context)
    {
        var result = await _svc.GetLastQuoteAsync(
            request.Symbol, request.Online, context.CancellationToken);

        if (result is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"No quote for {request.Symbol}"));

        return new QuoteReply
        {
            Symbol         = result.Symbol,
            Price          = (double)result.Price,
            Volume         = (double)result.Volume,
            Open           = (double)result.Open,
            High           = (double)result.High,
            Low            = (double)result.Low,
            PreviousClose  = (double)(result.PreviousClose ?? 0),
            Change         = (double)(result.Change ?? 0),
            ChangePercent  = result.ChangePercent ?? string.Empty,
            Timestamp      = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(result.Timestamp),
            ProviderId     = result.ProviderId ?? string.Empty,
            Settlement     = result.Settlement ?? string.Empty,
            DataSource     = MapDataSource(result.DataSource)
        };
    }

    public override async Task<DailyQuotesReply> GetDailyQuotes(
        GetDailyQuotesRequest request, ServerCallContext context)
    {
        var from = DateOnly.Parse(request.DateFrom);
        var to   = DateOnly.Parse(request.DateTo);

        var result = await _svc.GetDailyQuotesAsync(
            request.Symbol, from, to, request.Online, context.CancellationToken);

        var reply = new DailyQuotesReply { DataSource = MapDataSource(result.DataSource) };
        foreach (var q in result.Quotes)
            reply.Quotes.Add(MapDailyQuote(q));
        return reply;
    }

    public override async Task<IntradayQuotesReply> GetIntradayQuotes(
        GetIntradayQuotesRequest request, ServerCallContext context)
    {
        var date = DateOnly.Parse(request.Date);
        var result = await _svc.GetIntradayQuotesAsync(
            request.Symbol, date, request.Online, context.CancellationToken);

        var reply = new IntradayQuotesReply { DataSource = MapDataSource(result.DataSource) };
        foreach (var t in result.Ticks)
            reply.Ticks.Add(MapIntradayTick(t));
        return reply;
    }

    public override async Task<InstrumentsReply> SearchInstruments(
        SearchInstrumentsRequest request, ServerCallContext context)
    {
        var instruments = await _svc.SearchInstrumentsAsync(
            request.Query.Length > 0 ? request.Query : null,
            request.Market.Length > 0 ? request.Market : null,
            request.InstrumentType.Length > 0 ? request.InstrumentType : null,
            context.CancellationToken);

        var reply = new InstrumentsReply();
        foreach (var i in instruments)
            reply.Instruments.Add(new InstrumentReply
            {
                Id             = i.Id,
                Ticker         = i.Ticker,
                Description    = i.Description ?? string.Empty,
                Currency       = i.Currency ?? string.Empty,
                Market         = i.Market ?? string.Empty,
                InstrumentType = i.InstrumentType ?? string.Empty,
                ProviderId     = i.ProviderId ?? string.Empty
            });
        return reply;
    }

    public override async Task<DailyQuotesReply> GetQuotesByType(
        GetQuotesByTypeRequest request, ServerCallContext context)
    {
        var date = string.IsNullOrEmpty(request.Date)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.Parse(request.Date);

        var result = await _svc.GetQuotesByTypeAsync(
            request.InstrumentType, date, context.CancellationToken);

        var reply = new DailyQuotesReply { DataSource = MapDataSource(result.DataSource) };
        foreach (var q in result.Quotes)
            reply.Quotes.Add(MapDailyQuote(q));
        return reply;
    }

    public override async Task StreamQuotes(
        StreamQuotesRequest request,
        IServerStreamWriter<QuoteReply> responseStream,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;

        while (!ct.IsCancellationRequested)
        {
            foreach (var symbol in request.Symbols)
            {
                try
                {
                    var q = await _svc.GetLastQuoteAsync(symbol, online: true, ct);
                    if (q is not null)
                    {
                        await responseStream.WriteAsync(new QuoteReply
                        {
                            Symbol        = q.Symbol,
                            Price         = (double)q.Price,
                            Volume        = (double)q.Volume,
                            Open          = (double)q.Open,
                            High          = (double)q.High,
                            Low           = (double)q.Low,
                            PreviousClose = (double)(q.PreviousClose ?? 0),
                            Change        = (double)(q.Change ?? 0),
                            ChangePercent = q.ChangePercent ?? string.Empty,
                            Timestamp     = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(q.Timestamp),
                            ProviderId    = q.ProviderId ?? string.Empty,
                            Settlement    = q.Settlement ?? string.Empty,
                            DataSource    = MapDataSource(q.DataSource)
                        }, ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "StreamQuotes: error fetching {Symbol}", symbol);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    // ─── Mapping helpers ──────────────────────────────────────────────────────

    private static string MapDataSource(Models.DataSource ds) => ds switch
    {
        Models.DataSource.Online           => "online",
        Models.DataSource.Database         => "database",
        Models.DataSource.DatabaseFallback => "database_fallback",
        _                                  => "unknown"
    };

    private static DailyQuoteReply MapDailyQuote(Models.DailyQuoteResponse q) => new()
    {
        Symbol        = q.Symbol,
        Date          = q.Date.ToString("yyyy-MM-dd"),
        Open          = (double)q.Open,
        High          = (double)q.High,
        Low           = (double)q.Low,
        Close         = (double)q.Close,
        Volume        = (double)q.Volume,
        PreviousClose = (double)(q.PreviousClose ?? 0),
        Change        = (double)(q.Change ?? 0),
        ChangePercent = q.ChangePercent ?? string.Empty,
        Settlement    = q.Settlement ?? string.Empty,
        ProviderId    = q.ProviderId ?? string.Empty
    };

    private static IntradayQuoteReply MapIntradayTick(Models.IntradayTickResponse t) => new()
    {
        Symbol        = t.Symbol,
        Timestamp     = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(t.Timestamp),
        Price         = (double)t.Price,
        Volume        = (double)t.Volume,
        Open          = (double)(t.Open ?? 0),
        High          = (double)(t.High ?? 0),
        Low           = (double)(t.Low ?? 0),
        Change        = (double)(t.Change ?? 0),
        ChangePercent = t.ChangePercent ?? string.Empty,
        ProviderId    = t.ProviderId ?? string.Empty
    };
}

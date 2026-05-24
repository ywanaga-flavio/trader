using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trader.News.Data.Encryption;
using Trader.News.Data.Entities;
using Trader.News.Data.Enums;
using Trader.News.Worker.Analysis;
using Trader.News.Worker.Providers;
using Trader.News.RssTest;

// ---------------------------------------------------------------------------
// Trader.News.RssTest — Console app for testing RSS news extraction
// Usage:
//   dotnet run                        → uses built-in test feeds
//   dotnet run -- https://feed.url    → tests a custom URL
// ---------------------------------------------------------------------------

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// --- Logging ---------------------------------------------------------------
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole(o =>
    {
        o.SingleLine      = true;
        o.TimestampFormat = "HH:mm:ss ";
        o.ColorBehavior   = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
    })
    .SetMinimumLevel(LogLevel.Debug));

var logger = loggerFactory.CreateLogger<Program>();

// --- Test feeds ------------------------------------------------------------
var testFeeds = new List<TestFeed>
{
    // Argentina — economía y política
    new("🇦🇷  La Nación (economía)",   "https://www.lanacion.com.ar/arc/outboundfeeds/rss/category/economia/"),
    new("🇦🇷  Infobae (política)",      "https://www.infobae.com/arc/outboundfeeds/rss/category/politica/"),
    new("🇦🇷  Ámbito Financiero",       "https://www.ambito.com/rss.xml"),
    new("🇦🇷  Cronista",               "https://www.cronista.com/export/rss/politica-economia/"),
    new("🇦🇷  Infobae (economía)",      "https://www.infobae.com/arc/outboundfeeds/rss/category/economia/"),

    // USA / Global — politics and business
    new("🇺🇸  Reuters (business)",      "https://feeds.reuters.com/reuters/businessNews"),
    new("🇺🇸  NPR (politics)",          "https://feeds.npr.org/1014/rss.xml"),
    new("🌍  BBC (world)",              "https://feeds.bbci.co.uk/news/world/rss.xml"),
    new("🌍  BBC (business)",           "https://feeds.bbci.co.uk/news/business/rss.xml"),
    new("🇺🇸  Yahoo Finance",           "https://finance.yahoo.com/rss/topfinstories"),
};

// If the user passed URLs as args, test those instead
if (args.Length > 0)
{
    testFeeds.Clear();
    foreach (var url in args)
        testFeeds.Add(new TestFeed($"[arg] {url}", url));
}

// --- Stub encryption (no-op — public feeds need no credentials) -----------
var encryption = new NoOpEncryptionService();

// --- Provider --------------------------------------------------------------
var provider = new RssNewsSourceProvider(
    encryption,
    loggerFactory.CreateLogger<RssNewsSourceProvider>());

// --- NLI Analysis Service --------------------------------------------------
var modelDir = Path.GetFullPath("resources/models/classification");
var config   = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["NewsAnalysis:ModelPath"] = modelDir })
    .Build();
var analyser = new OnnxNliAnalysisService(config, loggerFactory.CreateLogger<OnnxNliAnalysisService>());

// --- Run -------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("  Trader.News.RssTest — RSS Extraction + NLI Analysis Console");
Console.WriteLine($"  {testFeeds.Count} feed(s) queued  |  Ctrl+C to abort");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

int totalItems    = 0;
int totalErrors   = 0;
int totalAnalyzed = 0;
long totalAnalysisMs = 0;
var grandClassCounts = new Dictionary<Trader.News.Data.Enums.NewsClassification, int>();
var grandSentCounts  = new Dictionary<Trader.News.Data.Enums.NewsValuation, int>();
var grandTimer = Stopwatch.StartNew();

foreach (var feed in testFeeds)
{
    if (cts.IsCancellationRequested) break;

    Console.WriteLine($"┌─ {feed.Label}");
    Console.WriteLine($"│  {feed.Url}");

    var source = new NewsSource
    {
        Id                    = 0,
        Name                  = feed.Label,
        Uri                   = feed.Url,
        Category              = NewsSourceCategory.Rss,
        IsEnabled             = true,
        SearchIntervalMinutes = 60,
    };

    var sw           = Stopwatch.StartNew();
    int count        = 0;
    int feedAnalyzed = 0;
    long feedAnalysisMs = 0;
    var feedClassCounts = new Dictionary<Trader.News.Data.Enums.NewsClassification, int>();
    var feedSentCounts  = new Dictionary<Trader.News.Data.Enums.NewsValuation, int>();
    var feedClassScores = new Dictionary<Trader.News.Data.Enums.NewsClassification, double>();
    var feedSentScores  = new Dictionary<Trader.News.Data.Enums.NewsValuation, double>();

    try
    {
        await foreach (var item in provider.FetchNewsAsync(source, cts.Token))
        {
            count++;
            totalItems++;

            var date    = item.NewsDate.HasValue
                              ? item.NewsDate.Value.ToString("yyyy-MM-dd HH:mm")
                              : "no date  ";
            var summary = item.Summary is { Length: > 0 }
                              ? Truncate(item.Summary, 120)
                              : "(no summary)";
            var uri     = item.Uri ?? "(no uri)";

            Console.WriteLine($"│");
            Console.WriteLine($"│  [{count:D3}] {date}  {Truncate(item.Title, 90)}");
            Console.WriteLine($"│       {uri}");
            Console.WriteLine($"│       {summary}");

            // Analyse every item
            var analysisSw = Stopwatch.StartNew();
            var result = await analyser.AnalyzeAsync(item.Title ?? string.Empty, item.Summary, cts.Token);
            analysisSw.Stop();
            feedAnalysisMs += analysisSw.ElapsedMilliseconds;

            if (result is not null)
            {
                var classId   = (Trader.News.Data.Enums.NewsClassification)result.ClassificationId;
                var sentiment = (Trader.News.Data.Enums.NewsValuation)result.SentimentId;

                feedAnalyzed++;
                feedClassCounts[classId]   = feedClassCounts.GetValueOrDefault(classId) + 1;
                feedSentCounts[sentiment]  = feedSentCounts.GetValueOrDefault(sentiment) + 1;
                feedClassScores[classId]   = feedClassScores.GetValueOrDefault(classId) + result.ClassificationScore;
                feedSentScores[sentiment]  = feedSentScores.GetValueOrDefault(sentiment) + result.SentimentScore;

                grandClassCounts[classId]  = grandClassCounts.GetValueOrDefault(classId) + 1;
                grandSentCounts[sentiment] = grandSentCounts.GetValueOrDefault(sentiment) + 1;
                totalAnalyzed++;
                totalAnalysisMs += analysisSw.ElapsedMilliseconds;

                Console.WriteLine($"│       📊 {classId,-15} score={result.ClassificationScore:F3}  │  {sentiment,-10} sent={result.SentimentScore:F3}  ({analysisSw.ElapsedMilliseconds} ms)");
            }
            else
            {
                Console.WriteLine($"│       📊 (analysis unavailable)");
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("│  ⚠ Cancelled.");
    }
    catch (Exception ex)
    {
        totalErrors++;
        logger.LogError(ex, "Feed failed: {Feed}", feed.Label);
    }

    sw.Stop();

    // Per-feed analysis summary
    Console.WriteLine($"│");
    Console.WriteLine($"│  ── Analysis summary ({feedAnalyzed}/{count} items, {feedAnalysisMs} ms) ──────────────────");
    if (feedAnalyzed > 0)
    {
        Console.WriteLine($"│  Category distribution:");
        foreach (var kv in feedClassCounts.OrderByDescending(x => x.Value))
        {
            var avgScore = feedClassScores[kv.Key] / kv.Value;
            var bar      = new string('█', kv.Value) + new string('░', feedAnalyzed - kv.Value);
            Console.WriteLine($"│    {kv.Key,-15} {kv.Value,3}  avg={avgScore:F3}  {bar}");
        }
        Console.WriteLine($"│  Sentiment distribution:");
        foreach (var kv in feedSentCounts.OrderByDescending(x => x.Value))
        {
            var avgScore = feedSentScores[kv.Key] / kv.Value;
            var bar      = new string('█', kv.Value) + new string('░', feedAnalyzed - kv.Value);
            Console.WriteLine($"│    {kv.Key,-15} {kv.Value,3}  avg={avgScore:F3}  {bar}");
        }
    }
    Console.WriteLine($"└─ {count} item(s) fetched in {sw.ElapsedMilliseconds} ms");
    Console.WriteLine();
}

grandTimer.Stop();
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine($"  Total: {totalItems} item(s)  |  {totalErrors} error(s)  |  {grandTimer.ElapsedMilliseconds} ms");
if (totalAnalyzed > 0)
{
    Console.WriteLine($"  Analyzed: {totalAnalyzed} item(s)  |  avg {totalAnalysisMs / totalAnalyzed} ms/item  |  total NLI {totalAnalysisMs} ms");
    Console.WriteLine();
    Console.WriteLine("  ── Grand total: Category ─────────────────────────────────");
    foreach (var kv in grandClassCounts.OrderByDescending(x => x.Value))
        Console.WriteLine($"    {kv.Key,-15} {kv.Value,4}  ({100.0 * kv.Value / totalAnalyzed:F1}%)");
    Console.WriteLine();
    Console.WriteLine("  ── Grand total: Sentiment ────────────────────────────────");
    foreach (var kv in grandSentCounts.OrderByDescending(x => x.Value))
        Console.WriteLine($"    {kv.Key,-15} {kv.Value,4}  ({100.0 * kv.Value / totalAnalyzed:F1}%)");
}
Console.WriteLine("═══════════════════════════════════════════════════════════════");

static string Truncate(string? s, int max) =>
    s is null ? string.Empty : s.Length <= max ? s : s[..max] + "…";

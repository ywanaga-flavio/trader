using Trader.MarketData.Data;
using Trader.MarketData.Worker.Configuration;
using Trader.MarketData.Worker.Workers;
using Trader.Providers.PortfolioPersonal;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

// ─── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<MarketDataWorkerOptions>(
    builder.Configuration.GetSection(MarketDataWorkerOptions.SectionName));

// ─── Database ─────────────────────────────────────────────────────────────────
var marketDataConnStr = builder.Configuration.GetConnectionString("MarketData")
    ?? throw new InvalidOperationException("Missing connection string 'MarketData'.");
var dbPassword = Environment.GetEnvironmentVariable("TRADER_QUOTAS_DB_PWD")
    ?? throw new InvalidOperationException("Missing environment variable 'TRADER_QUOTAS_DB_PWD'.");
marketDataConnStr += $";Password={dbPassword}";
builder.Services.AddMarketDataDb(marketDataConnStr);

// ─── Provider ─────────────────────────────────────────────────────────────────
builder.Services.AddPortfolioPersonalProviders(
    builder.Configuration.GetSection("PortfolioPersonal"));

// ─── Workers ──────────────────────────────────────────────────────────────────
builder.Services.AddHostedService<HistoricalQuoteWorker>();
builder.Services.AddHostedService<IntradayQuoteWorker>();

var host = builder.Build();
host.Run();

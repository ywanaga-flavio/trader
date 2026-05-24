using Hangfire;
using Hangfire.PostgreSql;
using Serilog;
using Trader.News.Data;
using Trader.News.Worker.Analysis;
using Trader.News.Worker.Jobs;
using Trader.News.Worker.Providers;
using Trader.News.Worker.Summarization;

// ─── Serilog bootstrap ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Trader.News.Worker");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName());

    // ─── Database ─────────────────────────────────────────────────────────────
    var newsConnStr = builder.Configuration.GetConnectionString("NewsDb")
        ?? throw new InvalidOperationException("Missing connection string 'NewsDb'.");
    var dbPassword = Environment.GetEnvironmentVariable("NEWS_DB_PWD")
        ?? throw new InvalidOperationException("Missing environment variable 'NEWS_DB_PWD'.");
    newsConnStr += $";Password={dbPassword}";
    builder.Services.AddNewsDb(newsConnStr);

    // ─── Hangfire ─────────────────────────────────────────────────────────────
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(newsConnStr)));

    builder.Services.AddHangfireServer(opts =>
    {
        opts.WorkerCount = 5;
        opts.Queues = ["news", "default"];
        opts.ServerName = $"trader-news-worker:{Environment.MachineName}";
    });

    // ─── News providers ───────────────────────────────────────────────────────
    builder.Services.AddSingleton<RssNewsSourceProvider>();
    builder.Services.AddSingleton<HtmlNewsSourceProvider>();
    builder.Services.AddSingleton<TwitterNewsSourceProvider>();
    builder.Services.AddSingleton<NewsSourceProviderFactory>();

    // ─── News analysis (ONNX NLI) ─────────────────────────────────────────────
    builder.Services.AddSingleton<INewsAnalysisService, OnnxNliAnalysisService>();

    // ─── Article summarizer (LLamaSharp) ─────────────────────────────────────
    builder.Services.Configure<ArticleSummarizerOptions>(
        builder.Configuration.GetSection("ArticleSummarizer"));
    builder.Services.AddSingleton<IArticleBodyFetcher, HtmlArticleBodyFetcher>();
    builder.Services.AddSingleton<IArticleSummarizerService, LlamaArticleSummarizerService>();

    // ─── Jobs ─────────────────────────────────────────────────────────────────
    builder.Services.AddScoped<NewsSchedulerJob>();
    builder.Services.AddScoped<ProcessNewsSourceJob>();

    var host = builder.Build();

    // Register recurring scheduler (every minute).
    using (var scope = host.Services.CreateScope())
    {
        var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurringJobs.AddOrUpdate<NewsSchedulerJob>(
            recurringJobId: "news-scheduler",
            methodCall: job => job.RunAsync(CancellationToken.None),
            cronExpression: Cron.Minutely(),
            queue: "news");
    }

    host.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Trader.News.Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


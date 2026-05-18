using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trader.Providers.PortfolioPersonal;

namespace Trader.IntegrationTests.PortfolioPersonal;

/// <summary>
/// Shared DI container for all PortfolioPersonal integration tests.
/// Credentials are read from appsettings.test.json (local only — never committed)
/// and from environment variables (CI/CD), in that order.
///
/// Tests are skipped automatically when credentials are not configured.
/// </summary>
public sealed class PortfolioPersonalFixture : IAsyncLifetime
{
    public IServiceProvider Services { get; private set; } = null!;
    public PortfolioPersonalOptions Options { get; private set; } = null!;
    public bool CredentialsAvailable { get; private set; }

    public Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(b => b
            .SetMinimumLevel(LogLevel.Debug)
            .AddConsole());

        services.AddPortfolioPersonalProviders(config.GetSection("PortfolioPersonal"));

        Services = services.BuildServiceProvider();

        Options = config
            .GetSection("PortfolioPersonal")
            .Get<PortfolioPersonalOptions>() ?? new PortfolioPersonalOptions();

        CredentialsAvailable =
            !string.IsNullOrWhiteSpace(Options.AuthorizedClient) &&
            !string.IsNullOrWhiteSpace(Options.ClientKey) &&
            !string.IsNullOrWhiteSpace(Options.ApiKey) &&
            !string.IsNullOrWhiteSpace(Options.ApiSecret);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Services is IAsyncDisposable ad) await ad.DisposeAsync();
    }
}

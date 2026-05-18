using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trader.Core.Providers;

namespace Trader.Providers.PortfolioPersonal;

/// <summary>
/// Extension method to register the PortfolioPersonal provider pair in the DI container.
///
/// Usage in Program.cs / Startup:
///   services.AddPortfolioPersonalProviders(configuration.GetSection("PortfolioPersonal"));
///
/// The Polly resilience pipeline is applied to the HttpClient factory at registration time.
/// Order placement (PlaceOrderAsync) does NOT participate in the retry pipeline —
/// retries on trade execution can cause duplicate orders.
/// </summary>
public static class PortfolioPersonalExtensions
{
    public static IServiceCollection AddPortfolioPersonalProviders(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfigurationSection configSection)
    {
        services.Configure<PortfolioPersonalOptions>(configSection);

        // Named HttpClient with resilience (used for market data & account reads)
        services.AddHttpClient<PortfolioPersonalAuthenticator>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<PortfolioPersonalOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        })
        .AddStandardResilienceHandler();

        services.AddHttpClient<PortfolioPersonalQuoteProvider>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<PortfolioPersonalOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        })
        .AddStandardResilienceHandler();

        // Broker HttpClient: NO standard retry — prevents duplicate orders.
        // Only timeout and circuit-breaker are added.
        services.AddHttpClient<PortfolioPersonalBrokerProvider>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<PortfolioPersonalOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
        });

        // Authenticator is singleton — shared token state across both providers
        services.AddSingleton<PortfolioPersonalAuthenticator>();

        services.AddSingleton<IQuoteProvider, PortfolioPersonalQuoteProvider>();
        services.AddSingleton<IBrokerProvider, PortfolioPersonalBrokerProvider>();

        return services;
    }
}

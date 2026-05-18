using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Trader.MarketData.Data;

/// <summary>
/// Registers <see cref="MarketDataDbContext"/> in the DI container.
///
/// Usage:
///   services.AddMarketDataDb(configuration.GetConnectionString("MarketData")!);
/// </summary>
public static class MarketDataDataExtensions
{
    public static IServiceCollection AddMarketDataDb(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<MarketDataDbContext>(opts =>
            opts.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(MarketDataDbContext).Assembly.FullName)));

        return services;
    }
}

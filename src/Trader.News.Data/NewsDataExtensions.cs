using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trader.News.Data.Encryption;

namespace Trader.News.Data;

/// <summary>
/// Extension methods to register News data-layer services in the DI container.
///
/// Usage:
///   services.AddNewsDb(configuration.GetConnectionString("NewsDb")!);
/// </summary>
public static class NewsDataExtensions
{
    public static IServiceCollection AddNewsDb(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<NewsDbContext>(opts =>
            opts.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(NewsDbContext).Assembly.FullName)));

        services.AddSingleton<IAesEncryptionService, AesEncryptionService>();

        return services;
    }
}

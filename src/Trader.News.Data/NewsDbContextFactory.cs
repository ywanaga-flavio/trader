using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Trader.News.Data;

/// <summary>
/// Design-time factory used by EF Core tools (migrations) when no running application
/// is available. Uses a local connection string; does not require environment variables.
/// </summary>
internal sealed class NewsDbContextFactory : IDesignTimeDbContextFactory<NewsDbContext>
{
    public NewsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NewsDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=trader_news;Username=trader_news;Password=changeme",
            npgsql => npgsql.MigrationsAssembly(typeof(NewsDbContext).Assembly.FullName));

        return new NewsDbContext(optionsBuilder.Options);
    }
}

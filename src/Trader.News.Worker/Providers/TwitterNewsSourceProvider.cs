using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Trader.News.Data.Entities;

namespace Trader.News.Worker.Providers;

/// <summary>
/// Scaffold provider for X (Twitter) social feed.
/// Full implementation requires a valid X API v2 Bearer Token.
///
/// Configure:
///   <list type="bullet">
///     <item><c>NewsSource.Username</c> — X API Bearer Token (stored encrypted)</item>
///   </list>
/// </summary>
public sealed class TwitterNewsSourceProvider : INewsSourceProvider
{
    private readonly ILogger<TwitterNewsSourceProvider> _logger;

    public TwitterNewsSourceProvider(ILogger<TwitterNewsSourceProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NewsFetchResult> FetchNewsAsync(
        NewsSource source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "TwitterNewsSourceProvider is not yet implemented. " +
            "Configure an X API v2 Bearer Token and implement this provider. " +
            "Source: {SourceId} ({SourceName})", source.Id, source.Name);

        // Yield nothing until implementation is complete.
        await Task.CompletedTask;
        yield break;
    }
}

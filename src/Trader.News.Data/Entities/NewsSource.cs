using Trader.News.Data.Enums;

namespace Trader.News.Data.Entities;

/// <summary>
/// Represents a configured news source that the worker polls periodically.
/// Passwords are stored AES-256 encrypted — never in plain text.
/// </summary>
public class NewsSource
{
    public int Id { get; set; }

    /// <summary>Display name for this source (e.g. "Reuters RSS", "Ámbito Financiero").</summary>
    public required string Name { get; set; }

    /// <summary>Optional human-readable description of the source.</summary>
    public string? Description { get; set; }

    /// <summary>Origin type used to select the appropriate <c>INewsSourceProvider</c>.</summary>
    public NewsSourceCategory Category { get; set; }

    /// <summary>Whether the worker should process this source.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>URI of the feed or page to fetch.</summary>
    public required string Uri { get; set; }

    /// <summary>Optional username for sources requiring HTTP basic auth.</summary>
    public string? Username { get; set; }

    /// <summary>
    /// AES-256 encrypted password. Use <c>IAesEncryptionService</c> to read/write.
    /// Null when no authentication is required.
    /// </summary>
    public string? PasswordEncrypted { get; set; }

    /// <summary>How often the worker should poll this source, in minutes.</summary>
    public int SearchIntervalMinutes { get; set; } = 60;

    /// <summary>UTC timestamp of the last successful execution. Null until first run.</summary>
    public DateTime? LastExecution { get; set; }

    /// <summary>Navigation property: news items fetched from this source.</summary>
    public ICollection<NewsItem> NewsItems { get; set; } = [];
}

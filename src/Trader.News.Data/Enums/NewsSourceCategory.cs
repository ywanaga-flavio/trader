namespace Trader.News.Data.Enums;

/// <summary>Category that describes the origin type of a news source.</summary>
public enum NewsSourceCategory
{
    /// <summary>RSS or Atom syndication feed.</summary>
    Rss = 1,

    /// <summary>Online media outlet (scraped HTML).</summary>
    Media = 2,

    /// <summary>Blog or personal publication (scraped HTML).</summary>
    Blog = 3,

    /// <summary>Social network feed (e.g. X/Twitter).</summary>
    Social = 4,
}

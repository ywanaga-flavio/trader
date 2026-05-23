namespace Trader.News.Data.Enums;

/// <summary>Thematic classification of a news item.</summary>
public enum NewsClassification
{
    /// <summary>Macroeconomic news, monetary policy, inflation, GDP.</summary>
    Economic = 1,

    /// <summary>Political events, elections, government decisions.</summary>
    Political = 2,

    /// <summary>Financial markets, equities, bonds, commodities.</summary>
    Market = 3,

    /// <summary>Technology sector, innovation, cybersecurity.</summary>
    Technology = 4,

    /// <summary>International events, geopolitics, trade relations.</summary>
    International = 5,

    /// <summary>Company-specific news, earnings, M&amp;A, management changes.</summary>
    Corporate = 6,
}

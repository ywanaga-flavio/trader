namespace Trader.News.Data.Enums;

/// <summary>Sentiment valuation assigned to a news item by the analysis pipeline.</summary>
public enum NewsValuation
{
    /// <summary>News with a positive market or economic impact.</summary>
    Positive = 1,

    /// <summary>News with a negative market or economic impact.</summary>
    Negative = 2,

    /// <summary>News with no clear directional impact.</summary>
    Neutral = 3,
}

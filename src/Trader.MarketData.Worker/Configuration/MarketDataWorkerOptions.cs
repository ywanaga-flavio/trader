namespace Trader.MarketData.Worker.Configuration;

/// <summary>Root configuration for all market-data providers polled by the worker.</summary>
public class MarketDataWorkerOptions
{
    public const string SectionName = "MarketData";

    /// <summary>One entry per provider to poll.</summary>
    public List<ProviderConfig> Providers { get; set; } = [];
}

/// <summary>Per-provider polling configuration.</summary>
public class ProviderConfig
{
    /// <summary>Must match a registered <c>IQuoteProvider</c> ProviderId (e.g. "portfoliopersonal").</summary>
    public required string ProviderId { get; set; }

    /// <summary>When false, no workers start for this provider.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Markets whose instruments will be discovered and polled.</summary>
    public List<string> Markets { get; set; } = [];

    public HistoricalConfig Historical { get; set; } = new();
    public IntradayConfig Intraday { get; set; } = new();

    /// <summary>Trading hours per market. Used to gate intraday polling.</summary>
    public List<MarketHoursConfig> MarketHours { get; set; } = [];
}

/// <summary>Configuration for the historical (end-of-day) back-fill worker.</summary>
public class HistoricalConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Fetch history starting from this date (ISO 8601 date, e.g. "2020-01-01").</summary>
    public DateOnly FromDate { get; set; } = new(2020, 1, 1);

    /// <summary>Number of parallel tasks used to fetch instrument history.</summary>
    public int ThreadCount { get; set; } = 4;
}

/// <summary>Configuration for the intraday (live tick) polling worker.</summary>
public class IntradayConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds between each polling round (default 30).</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>Number of parallel tasks used per polling round.</summary>
    public int ThreadCount { get; set; } = 2;
}

/// <summary>Trading-hours window for one market.</summary>
public class MarketHoursConfig
{
    /// <summary>Market code (e.g. "BYMA", "NYSE").</summary>
    public required string Market { get; set; }

    /// <summary>IANA time-zone identifier (e.g. "America/Argentina/Buenos_Aires").</summary>
    public required string TimeZone { get; set; }

    /// <summary>Market open time (HH:mm).</summary>
    public TimeOnly OpenTime { get; set; }

    /// <summary>Market close time (HH:mm).</summary>
    public TimeOnly CloseTime { get; set; }

    /// <summary>Days on which the market is open.</summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday
    ];

    /// <summary>Returns true when the market is currently open.</summary>
    public bool IsOpen(DateTimeOffset utcNow)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var tod = TimeOnly.FromDateTime(local.DateTime);

        return DaysOfWeek.Contains(local.DayOfWeek) && tod >= OpenTime && tod < CloseTime;
    }
}

namespace Trader.Providers.PortfolioPersonal;

/// <summary>
/// Credentials and settings for the Portfolio Personal (PPI) API.
///
/// Configuration section: "PortfolioPersonal"
///
/// Required credentials (never commit real values to VCS — use environment variables):
///   PORTFOLIOPERSONAL__AUTHORIZEDCLIENT
///   PORTFOLIOPERSONAL__CLIENTKEY
///   PORTFOLIOPERSONAL__APIKEY
///   PORTFOLIOPERSONAL__APISECRET
///
/// Obtain credentials by registering an API client at:
///   https://itatppi.github.io/ppi-official-api-docs/api/documentacionRest
/// </summary>
public class PortfolioPersonalOptions
{
    public const string SectionName = "PortfolioPersonal";

    // --- Credentials (all required) ---

    /// <summary>Authorized Client ID for the API (header: AuthorizedClient).</summary>
    public string AuthorizedClient { get; set; } = string.Empty;

    /// <summary>Client Key for the API (header: ClientKey).</summary>
    public string ClientKey { get; set; } = string.Empty;

    /// <summary>API Key (header: ApiKey) — used only for the initial login call.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API Secret (header: ApiSecret) — used only for the initial login call.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Default trading account number used when no accountNumber is supplied per-call.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    // --- Connectivity ---

    /// <summary>Base URL of the PPI API. Override to target sandbox or production.</summary>
    public string BaseUrl { get; set; } = "https://clientapi_sandbox.portfoliopersonal.com";

    // --- Quote streaming ---

    /// <summary>Polling interval when streaming quotes (REST fallback, no WebSocket).</summary>
    public TimeSpan QuotePollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    // --- Token refresh ---

    /// <summary>
    /// How far in advance of expiry to refresh the access token.
    /// Default: 2 minutes before expiry.
    /// </summary>
    public TimeSpan TokenRefreshBuffer { get; set; } = TimeSpan.FromMinutes(2);

    // --- Resilience (applied at DI registration site) ---

    public int MaxRetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 15;
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 60;
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trader.Providers.PortfolioPersonal.Models;

namespace Trader.Providers.PortfolioPersonal;

/// <summary>
/// Manages the Portfolio Personal JWT session: initial login and proactive token refresh.
///
/// Authentication flow:
///   1. POST /api/1.0/Account/LoginApi  — headers: AuthorizedClient, ClientKey, ApiKey, ApiSecret
///      → returns Token[] (array with one element)
///   2. All subsequent requests: Authorization: Bearer {accessToken}
///                               AuthorizedClient, ClientKey headers
///   3. POST /api/1.0/Account/RefreshToken — body: { refreshToken }
///                                           headers: AuthorizedClient, ClientKey
///      → returns Token[] with new accessToken + refreshToken
///
/// This class is registered as a singleton and shared between QuoteProvider and BrokerProvider.
/// </summary>
public sealed class PortfolioPersonalAuthenticator : IAsyncDisposable
{
    private readonly PortfolioPersonalOptions _opts;
    private readonly HttpClient _http;
    private readonly ILogger<PortfolioPersonalAuthenticator> _logger;

    private PpiToken? _currentToken;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public PortfolioPersonalAuthenticator(
        IOptions<PortfolioPersonalOptions> options,
        HttpClient http,
        ILogger<PortfolioPersonalAuthenticator> logger)
    {
        _opts = options.Value;
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Returns a valid Bearer token, refreshing or re-logging in if necessary.
    /// Thread-safe.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        // Fast path: token is still valid
        if (_currentToken is not null && !IsExpiringSoon(_currentToken))
            return _currentToken.AccessToken!;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_currentToken is not null && !IsExpiringSoon(_currentToken))
                return _currentToken.AccessToken!;

            if (_currentToken?.RefreshToken is not null)
            {
                _logger.LogInformation("Refreshing PPI access token");
                _currentToken = await RefreshTokenAsync(_currentToken.RefreshToken, ct);
            }
            else
            {
                _logger.LogInformation("Logging in to PPI API");
                _currentToken = await LoginAsync(ct);
            }

            return _currentToken.AccessToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Adds the required PPI headers to an HttpRequestMessage.
    /// Call this inside every request builder.
    /// </summary>
    public void AddClientHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("AuthorizedClient", _opts.AuthorizedClient);
        request.Headers.Add("ClientKey", _opts.ClientKey);
    }

    /// <summary>
    /// Builds an authorized HttpRequestMessage with Bearer token + client headers.
    /// </summary>
    public async Task<HttpRequestMessage> BuildAuthorizedRequestAsync(
        HttpMethod method, string relativeUrl, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        AddClientHeaders(request);
        return request;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<PpiToken> LoginAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/1.0/Account/LoginApi");
        request.Headers.Add("AuthorizedClient", _opts.AuthorizedClient);
        request.Headers.Add("ClientKey", _opts.ClientKey);
        request.Headers.Add("ApiKey", _opts.ApiKey);
        request.Headers.Add("ApiSecret", _opts.ApiSecret);

        var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "LoginApi", ct);

        var tokens = await response.Content.ReadFromJsonAsync<PpiToken[]>(ct)
                     ?? throw new InvalidOperationException("PPI LoginApi returned empty token response.");

        var token = tokens[0];
        _logger.LogInformation(
            "PPI login successful. Token expires at {ExpiresAt}", token.ExpirationDate);

        return token;
    }

    private async Task<PpiToken> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/1.0/Account/RefreshToken");
        request.Headers.Add("AuthorizedClient", _opts.AuthorizedClient);
        request.Headers.Add("ClientKey", _opts.ClientKey);
        request.Content = JsonContent.Create(new PpiRefreshRequest { RefreshToken = refreshToken });

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PPI token refresh failed ({Status}), falling back to full login",
                response.StatusCode);
            // Invalidate so next call triggers full login
            _currentToken = null;
            return await LoginAsync(ct);
        }

        var tokens = await response.Content.ReadFromJsonAsync<PpiToken[]>(ct)
                     ?? throw new InvalidOperationException("PPI RefreshToken returned empty response.");

        var token = tokens[0];
        _logger.LogInformation(
            "PPI token refreshed. New expiry: {ExpiresAt}", token.ExpirationDate);

        return token;
    }

    private bool IsExpiringSoon(PpiToken token) =>
        token.ExpirationDate - DateTimeOffset.UtcNow < _opts.TokenRefreshBuffer;

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"PPI {operation} failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
    }

    public ValueTask DisposeAsync()
    {
        _refreshLock.Dispose();
        return ValueTask.CompletedTask;
    }
}

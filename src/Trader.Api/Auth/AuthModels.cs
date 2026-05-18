namespace Trader.Api.Auth;

/// <summary>Login request payload.</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>Successful login response containing the bearer token.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAt);

using Microsoft.AspNetCore.Mvc;
using Trader.Api.Auth;

namespace Trader.Api.Controllers;

/// <summary>Issues JWT bearer tokens for authenticated users.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly JwtTokenService _tokenService;

    public AuthController(JwtTokenService tokenService) => _tokenService = tokenService;

    /// <summary>
    /// Authenticate with username and password and receive a JWT bearer token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>Bearer token and expiry, or 401 on invalid credentials.</returns>
    [HttpPost("token")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Token([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        var result = _tokenService.Authenticate(request.Username, request.Password);

        if (result is null)
            return Unauthorized();

        return Ok(result);
    }
}

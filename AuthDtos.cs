/// <summary>Credentials used to create an account.</summary>
public sealed record RegisterRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

/// <summary>Credentials used to authenticate an account.</summary>
public sealed record LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

/// <summary>A bearer token issued to an authenticated user.</summary>
public sealed record AuthTokenResponse(
    int UserId,
    string Username,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc);

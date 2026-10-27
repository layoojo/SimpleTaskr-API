using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public sealed class JwtTokenService(string issuer, string audience, SymmetricSecurityKey signingKey)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public AuthTokenResponse CreateToken(User user)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AuthTokenResponse(
            user.Id,
            user.Username!,
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAtUtc);
    }
}

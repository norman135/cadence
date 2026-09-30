using System.Text;
using Cadence.Application.Common.Abstractions;
using Cadence.Application.Features.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Cadence.Infrastructure.Identity;

/// <summary>Creates short-lived, HMAC-SHA256-signed JWT access tokens.</summary>
internal sealed class AccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider timeProvider)
{
    private readonly AuthOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _signingCredentials =
        new(CreateSigningKey(options.Value.SigningKey), SecurityAlgorithms.HmacSha256);

    public static SymmetricSecurityKey CreateSigningKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));

    public AccessToken Issue(AuthUser user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + _options.AccessTokenLifetime;

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Name] = user.DisplayName,
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
            },
        });

        return new AccessToken(token, expiresAt);
    }
}

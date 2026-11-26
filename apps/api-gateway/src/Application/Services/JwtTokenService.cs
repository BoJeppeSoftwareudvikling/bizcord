using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace Bizcord.ApiGateway.Services;


public class CreateTokenOptions
{
    public Dictionary<string, string> Claims { get; set; } = new();
}

public class JwtTokenService
{
    private readonly string _authenticationProviderKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(
        string authenticationProviderKey,
        string issuer,
        string audience)
    {
        _authenticationProviderKey = authenticationProviderKey;
        _issuer = issuer;
        _audience = audience;
    }

    public AuthenticationToken CreateToken(CreateTokenOptions options)
    {
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authenticationProviderKey));
        var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>();
        foreach (var claim in options.Claims)
        {
            if (claim.Key == "sub")
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Sub, claim.Value));
                continue;
            }
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        var tokenOptions = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            signingCredentials: signingCredentials,
            expires: DateTime.UtcNow.AddHours(1),
            claims: claims
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenOptions);
        var authToken = new AuthenticationToken
        {
            Value = tokenString
        };

        return authToken;
    }
}

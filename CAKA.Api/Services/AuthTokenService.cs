using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CAKA.Api.Services;

public class AuthTokenService : IAuthTokenService
{
    private readonly IConfiguration _config;

    public AuthTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(string userName, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Key"] ?? "CAKA-Jwt-Secret-Key-Min-32-Chars-Long!!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expMinutes = int.TryParse(_config["Jwt:ExpirationMinutes"], out var m) ? m : 480;

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, userName),
            new Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

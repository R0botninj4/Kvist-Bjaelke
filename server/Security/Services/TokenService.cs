using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataAccess.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Security.Services;

public class TokenService(IConfiguration configuration)
{
    public const string Issuer = "kvist-bjaelke", Audience = "kvist-bjaelke-web";

    public string Create(User user, LoginSession session)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("version", user.SecurityVersion.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, session.Id)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtSecret"]!));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Issuer, Audience, claims, DateTime.UtcNow, session.ExpiresAt,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }
}

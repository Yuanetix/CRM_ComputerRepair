using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CRM_ComputerRepair.domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace CRM_ComputerRepair.api.Services;

/// <summary>
/// Issues signed JWT bearer tokens for authenticated CRM users.
/// The role is embedded as a claim so controllers can enforce
/// role-based access via [Authorize(Roles = ...)].
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(User user, IList<string> roles)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = jwtSection["Issuer"] ?? "Fixory";
        var audience = jwtSection["Audience"] ?? "FixoryClient";
        var expiresMinutes = int.TryParse(jwtSection["ExpiresMinutes"], out var m)
            ? m
            : 480;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("FullName", $"{user.FirstName} {user.LastName}".Trim()),
            new("CompanyId", "1")
        };

        foreach (var role in roles.Distinct())
            claims.Add(new Claim(ClaimTypes.Role, role));

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
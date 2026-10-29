using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EmployeeLeaveApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace EmployeeLeaveApi.Services;

public interface ITokenService
{
    (string Token, DateTime Expiry) CreateToken(User user, int? employeeId = null);
}

public class TokenService : ITokenService
{
    public const string EmployeeIdClaim = "employeeId";
    private readonly IConfiguration _config;
    public TokenService(IConfiguration config) => _config = config;

    public (string Token, DateTime Expiry) CreateToken(User user, int? employeeId = null)
    {
        var jwt = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpiryMinutes"] ?? "60"));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (employeeId.HasValue) claims.Add(new Claim(EmployeeIdClaim, employeeId.Value.ToString()));
        var token = new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims,
            expires: expiry,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }
}

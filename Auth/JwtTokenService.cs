using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SimpleeCommerceApp.Auth
{
    public interface IJwtTokenService
    {
        string CreateToken(Guid userId, string username);
    }

    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _config;
        public JwtTokenService(IConfiguration config) => _config = config;

        public string CreateToken(Guid userId, string username)
        {
            var jwt = _config.GetSection("Jwt");
            var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
            var issuer = jwt["Issuer"] ?? "SimpleCommerce";
            var audience = jwt["Audience"] ?? "SimpleCommerce";

            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

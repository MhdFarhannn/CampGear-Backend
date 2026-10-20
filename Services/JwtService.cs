using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CampGearBackend.Services
{
    public class JwtService
    {
        #pragma warning disable CS8602
        private readonly string _secret = Env.Value["Jwt:Secret"] ?? string.Empty;
        private readonly string _issuer = Env.Value["Jwt:Issuer"] ?? string.Empty;
        private readonly string _audience = Env.Value["Jwt:Audience"] ?? string.Empty;
        private readonly int _expiryMinutes = int.Parse(Env.Value["Jwt:ExpiryMinutes"] ?? "120");
        #pragma warning restore CS8602

        public (string Token, DateTime ExpiresAt) CreateToken(uint id, string nama, string role)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(_expiryMinutes);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Name, nama),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}

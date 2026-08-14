using BE_ZSM.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BE_ZSM.Services
{
    public class JwtService
    {
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly int _expireMinutes;

        public JwtService()
        {
            _jwtKey = GetRequiredEnvironmentVariable("JWT_KEY");
            _jwtIssuer = GetRequiredEnvironmentVariable("JWT_ISSUER");
            _jwtAudience = GetRequiredEnvironmentVariable("JWT_AUDIENCE");

            var expireMinutes = GetRequiredEnvironmentVariable("JWT_EXPIRE_MINUTES");

            if (!int.TryParse(expireMinutes, out _expireMinutes))
            {
                throw new InvalidOperationException(
                    "JWT_EXPIRE_MINUTES must be a valid number."
                );
            }
        }

        public string GenerateToken(User user)
        {
            var roleName = user.Role.Name.ToString();

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    user.Username
                ),

                new Claim(
                    ClaimTypes.Email,
                    user.Email
                ),

                new Claim(
                    ClaimTypes.Role,
                    roleName
                ),

                new Claim(
                    "role",
                    roleName
                )
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtKey)
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_expireMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);

            return Convert.ToBase64String(randomBytes);
        }

        public DateTime GetRefreshTokenExpiration()
        {
            return DateTime.UtcNow.AddDays(7);
        }

        private static string GetRequiredEnvironmentVariable(string key)
        {
            return Environment.GetEnvironmentVariable(key)
                ?? throw new InvalidOperationException(
                    $"{key} is missing."
                );
        }
    }
}
using BMIRussian_ru.Data;
using BMIRussian_ru.Exceptions;
using BMIRussian_ru.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sibvic.AuthLib;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BMIRussian_ru.Logic
{
    public record AuthOptions(string Key);

    public class AuthLogic(ApplicationDbContext context, AuthOptions options)
    {
        public User FindUser(string? id, CredentialsSource source)
        {
            return context.UserCredentianls
                .Include(uc => uc.User)
                .Where(uc => uc.SourceId == id && uc.Source == source)
                .Select(uc => uc.User)
                .FirstOrDefault();
        }

        public User? RegisterUser(string id, string? first_name, string? last_name, string? username, string? photo_url, string? auth_date, string? hash, CredentialsSource telegram)
        {
            var user = context.Users.Add(new User()
            {
                FirstName = first_name,
                LastName = last_name,
                Nickname = username ?? id,
            });
            context.UserCredentianls.Add(new UserCredentianl()
            {
                SourceId = id,
                Source = CredentialsSource.Telegram,
                User = user.Entity
            });
            context.SaveChanges();
            user.Reload();

            return user.Entity;
        }

        public string GenerateToken(User? user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(options.Key);
            
            var claims = new List<Claim> { new Claim("id", user.Id.ToString()) };
            
            // Add role claims from UserRoles table
            var roles = context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role)
                .ToList();
            
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public string AuthenticateFromTelegramBot(FromTelegramBotRequest request)
        {
            // Find user by telegram id
            User? user = FindUser(request.TelegramId, CredentialsSource.Telegram);
            if (user == null)
            {
                throw new UserNotFoundException();
            }

            // Check if temporary token exists and is valid
            var userToken = context.UserToken
                .Where(ut => ut.Token == request.TemporaryToken && ut.UserId == user.Id)
                .FirstOrDefault();

            if (userToken == null)
            {
                throw new InvalidTokenException();
            }

            if (userToken.ValidTill < DateTime.UtcNow)
            {
                throw new TokenExpiredException();
            }

            // Generate and return bearer token
            return GenerateToken(user);
        }
    }
}

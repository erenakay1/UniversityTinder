using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UniversityTinder.Models;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Services
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtOptions _jwtOptions;
        public JwtTokenGenerator(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        public string GenerateToken(ApplicationUser applicationUser, UserProfile profile, IEnumerable<string> roles)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtOptions.Secret);

            var claimList = new List<Claim>
            {
                // ===== ApplicationUser bilgileri =====
                new Claim(JwtRegisteredClaimNames.Email, applicationUser.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Sub, applicationUser.Id),
                new Claim(JwtRegisteredClaimNames.Name, $"{applicationUser.FirstName} {applicationUser.LastName}"),
                new Claim("UserId", applicationUser.Id),
                new Claim("UserName", applicationUser.UserName ?? ""),
                new Claim("FirstName", applicationUser.FirstName ?? ""),
                new Claim("LastName", applicationUser.LastName ?? ""),
                new Claim("Gender", applicationUser.Gender ?? ""),
                
                // University bilgileri
                new Claim("UniversityName", applicationUser.UniversityName ?? ""),
                new Claim("UniversityDomain", applicationUser.UniversityDomain ?? ""),
                new Claim("IsUniversityVerified", applicationUser.IsUniversityVerified.ToString()),


                // ===== UserProfile bilgileri =====
                new Claim("ProfileId", profile?.ProfileId.ToString() ?? "0"),
                new Claim("DisplayName", profile?.DisplayName ?? ""),
                new Claim("ProfileImageUrl", profile?.ProfileImageUrl ?? ""),
        
                // Profile completion & verification
                new Claim("IsProfileCompleted", profile?.IsProfileCompleted.ToString() ?? "false"),
                new Claim("IsPhotoVerified", profile?.IsPhotoVerified.ToString() ?? "false"),
                new Claim("ProfileCompletionScore", profile?.ProfileCompletionScore.ToString() ?? "0"),
        
                // Stats & counts
                new Claim("TotalMatchCount", profile?.TotalMatchCount.ToString() ?? "0"),
                new Claim("TotalLikesReceived", profile?.TotalLikesReceived.ToString() ?? "0"),
                new Claim("MatchesCount", profile?.MatchesList?.Count.ToString() ?? "0"),
                new Claim("LikesGiven", profile?.LikedUsersList?.Count.ToString() ?? "0"),
                new Claim("LikesReceived", profile?.ReceivedLikesList?.Count.ToString() ?? "0"),
        
                // Premium status
                new Claim("IsPremium", profile?.IsPremium.ToString() ?? "false"),
                new Claim("PremiumExpiresAt", profile?.PremiumExpiresAt?.ToString("o") ?? ""),
                new Claim("UnlimitedSwipes", profile?.UnlimitedSwipes.ToString() ?? "0"),
                new Claim("BoostsRemaining", profile?.BoostsRemaining.ToString() ?? "0"),
        
                // Swipe limits
                new Claim("DailySwipeCount", profile?.DailySwipeCount.ToString() ?? "0"),
                new Claim("SuperLikeCount", profile?.SuperLikeCount.ToString() ?? "1"),
        
                // Privacy settings
                new Claim("ShowMyUniversity", profile?.ShowMyUniversity.ToString() ?? "true"),
                new Claim("ShowMeOnApp", profile?.ShowMeOnApp.ToString() ?? "true"),
        
                // Dating preferences
                new Claim("InterestedIn", profile?.InterestedIn ?? "Everyone"),
                new Claim("AgeRangeMin", profile?.AgeRangeMin.ToString() ?? "18"),
                new Claim("AgeRangeMax", profile?.AgeRangeMax.ToString() ?? "30"),
        
                // Location
                new Claim("City", profile?.City ?? ""),
                new Claim("MaxDistance", profile?.MaxDistance.ToString() ?? "50"),
        
                // Social
                new Claim("InstagramUsername", profile?.InstagramUsername ?? ""),
        
                // Account status
                new Claim("IsActive", profile?.IsActive.ToString() ?? "true"),
                new Claim("LastActiveAt", profile?.LastActiveAt?.ToString("o") ?? "")
                
                // Hobbiler (JSON string olarak)
                //new Claim("Hobbies", profile?.Hobbies != null ? JsonSerializer.Serialize(profile.Hobbies) : "[]")
            };

            claimList.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Audience = _jwtOptions.Audience,
                Issuer = _jwtOptions.Issuer,
                Subject = new ClaimsIdentity(claimList),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}

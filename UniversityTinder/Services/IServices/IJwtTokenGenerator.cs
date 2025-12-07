using UniversityTinder.Models;

namespace UniversityTinder.Services.IServices
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(ApplicationUser applicationUser, UserProfile profile, IEnumerable<string> roles);
    }
}

using Microsoft.AspNetCore.Identity;
using static Utility.SD;

namespace UniversityTinder.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty; // "Ramiz"
        public string LastName { get; set; } = string.Empty; // "Kadayıfcı"
        public DateTime DateOfBirth { get; set; }
        public GenderType Gender { get; set; } // "Male", "Female", "Other"

        // University Verification
        public string Email { get; set; } = string.Empty; // "bilgiedu.net"
        public string UniversityDomain { get; set; } = string.Empty; // "bilgiedu.net"
        public string UniversityName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime? EmailVerifiedAt { get; set; }
        public DateTime? LastVerificationCheck { get; set; }
        public bool IsUniversityVerified { get; set; } = false;
    }
}

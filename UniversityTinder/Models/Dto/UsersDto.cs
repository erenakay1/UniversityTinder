
namespace UniversityTinder.Models.Dto
{
    public class UsersDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty; // Compatibility için
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; // "Ramiz K."
        public string? Gender { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfileImageUrl { get; set; }
        public List<string>? Roles { get; set; }
        public bool? Lock_Unlock { get; set; }

        // Dating app için ek alanlar
        public int? Age { get; set; }
        public string? UniversityName { get; set; }
        public bool IsVerified { get; set; } = false;
        public bool HasUnreadMessages { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public bool IsPassed { get; set; } = false;
        public bool IsSuperLike { get; set; } = false;
        public bool IsProfileCreated { get; set; } = false;
    }
}

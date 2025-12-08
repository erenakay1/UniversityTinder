using System.ComponentModel.DataAnnotations.Schema;

namespace UniversityTinder.Models.Dto
{
    [NotMapped]  // ⭐ Bu satırı ekle
    public class UsersDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfileImageUrl { get; set; }
        public List<string>? Roles { get; set; }
        public bool? Lock_Unlock { get; set; }
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
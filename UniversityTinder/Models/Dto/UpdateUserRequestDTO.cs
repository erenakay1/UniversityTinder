using Utility;
using static Utility.SD;

namespace UniversityTinder.Models.Dto
{
    public class UpdateUserRequestDTO
    {
        public string UserId { get; set; }
        public string? Name { get; set; }
        public string DisplayName { get; set; }
        public string? Surname { get; set; }
        public GenderType? Gender { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }
}

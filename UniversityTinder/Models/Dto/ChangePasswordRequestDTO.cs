namespace UniversityTinder.Models.Dto
{
    public class ChangePasswordRequestDTO
    {
        public string UserId { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
}

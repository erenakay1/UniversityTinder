namespace UniversityTinder.Models.Dto
{
    public class DeleteUserRequestDTO
    {
        public string UserId { get; set; }
        public string Password { get; set; } // Güvenlik için şifre doğrulama
    }
}

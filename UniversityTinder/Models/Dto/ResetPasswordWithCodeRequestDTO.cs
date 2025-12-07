namespace UniversityTinder.Models.Dto
{
    public class ResetPasswordWithCodeRequestDTO
    {
        public string Email { get; set; }
        public string ResetCode { get; set; }
        public string NewPassword { get; set; }
    }
}

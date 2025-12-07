namespace UniversityTinder.Models.Dto
{
    // VerifyEmailWithCodeRequestDTO.cs
    namespace UniversityTinder.Models.Dto
    {
        public class VerifyEmailWithCodeRequestDTO
        {
            public string Email { get; set; }
            public string VerificationCode { get; set; }
        }
    }

    // ResendVerificationCodeRequestDTO.cs
    namespace UniversityTinder.Models.Dto
    {
        public class ResendVerificationCodeRequestDTO
        {
            public string Email { get; set; }
        }
    }
}

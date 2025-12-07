namespace UniversityTinder.Models.Dto
{
    public class LoginResponseDto
    {
        public UserDto User { get; set; }
        public string Token { get; set; }
        public bool? IsSuccess { get; set; }
        public string Message { get; set; }
    }
}

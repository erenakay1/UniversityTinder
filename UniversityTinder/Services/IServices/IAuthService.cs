using UniversityTinder.Models.Dto;
using UniversityTinder.Models.Dto.UniversityTinder.Models.Dto;

namespace UniversityTinder.Services.IServices
{
    public interface IAuthService
    {
        Task<LoginResponseDto> Login(LoginRequestDTO loginRequestDTO);
        Task<LoginResponseDto> Register(RegistrationRequestDTO registrationRequestDTO);
        Task<string> DeleteUser(string userId, string password);
        Task<UserDto> GetUserById(string userId);
        Task<UserDto> UpdateUser(UpdateUserRequestDTO updateRequest);
        Task<UserDto> ChangePassword(ChangePasswordRequestDTO changePasswordRequest);
        Task<string> ForgotPassword(ForgotPasswordRequestDTO forgotPasswordRequest);
        Task<string> ResetPasswordWithCode(ResetPasswordWithCodeRequestDTO resetPasswordRequest);
        Task<bool> AssignRole(string email, string roleName);
        Task<string> VerifyEmailWithCode(VerifyEmailWithCodeRequestDTO verifyRequest);
        Task<string> ResendVerificationCode(ResendVerificationCodeRequestDTO resendRequest);
    }
}

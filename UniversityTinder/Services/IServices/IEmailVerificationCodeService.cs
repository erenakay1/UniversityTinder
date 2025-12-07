namespace UniversityTinder.Services.IServices
{
    public interface IEmailVerificationCodeService
    {
        Task<string> GenerateVerificationCodeAsync(string email);
        Task<bool> ValidateVerificationCodeAsync(string email, string code);
        Task InvalidateVerificationCodeAsync(string email);
    }
}
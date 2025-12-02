namespace UniversityTinder.Services.IServices
{
    public interface IPasswordResetCodeService
    {
        Task<string> GenerateResetCodeAsync(string userId);
        Task<bool> ValidateResetCodeAsync(string email, string code);
        Task InvalidateResetCodeAsync(string email);
    }
}

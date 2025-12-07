namespace UniversityTinder.Services.IServices
{
    public interface IEmailService
    {
        Task SendPasswordResetCodeAsync(string email, string resetToken);
        Task SendEmailAsync(string email, string subject, string htmlMessage);
        Task SendVerificationEmailAsync(string email, string token, string userId);
        Task SendEmailVerificationCodeAsync(string email, string verificationCode, string userName); // YENİ

    }
}

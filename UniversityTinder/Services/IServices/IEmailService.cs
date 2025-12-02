namespace UniversityTinder.Services.IServices
{
    public interface IEmailService
    {
        Task SendPasswordResetCodeAsync(string email, string resetToken);
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }
}

using System.Threading.Tasks;

namespace HRMS.Infrastructure.Services
{
    public interface IEmailService
    {
        Task<bool> SendWelcomeCredentialsAsync(string toEmail, string fullName, string username, string tempPassword, string loginUrl);
        Task<bool> SendPasswordResetLinkAsync(string toEmail, string fullName, string resetUrl);
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? plainTextBody);
    }
}

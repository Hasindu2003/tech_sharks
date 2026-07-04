namespace HRMS.Application.Notifications
{
    // Extension seam for real SMTP — swap the DI registration for a real implementation
    // once SMTP credentials are available. Until then, NoOpEmailSender just logs.
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string body);
    }
}

using Microsoft.Extensions.Logging;

namespace HRMS.Application.Notifications
{
    public class NoOpEmailSender : IEmailSender
    {
        private readonly ILogger<NoOpEmailSender> _logger;

        public NoOpEmailSender(ILogger<NoOpEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string toEmail, string subject, string body)
        {
            _logger.LogInformation("Email (not sent — no SMTP configured) to {ToEmail}: {Subject}", toEmail, subject);
            return Task.CompletedTask;
        }
    }
}

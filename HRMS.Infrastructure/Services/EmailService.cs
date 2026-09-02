using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendWelcomeCredentialsAsync(string toEmail, string fullName, string username, string tempPassword, string loginUrl)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;

            var subject = "Welcome to Kanrich HRMS - Your Account Credentials";

            var plainText = $@"Hello {fullName},

Welcome to Kanrich Finance Limited! Your official employee account has been created on the Kanrich HRMS portal.

Your login credentials:
Username: {username}
Temporary Password: {tempPassword}

Sign In Link:
{loginUrl}

Security Notice:
This temporary password was generated for your first login. You will be prompted to create your own private, secure password immediately upon signing in.

Best regards,
Kanrich Finance HR Team
(This is an automated system notification)";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 30px auto; background-color: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
        .header {{ background-color: #ffffff; padding: 28px 24px 20px; text-align: center; border-bottom: 2px solid #e2e8f0; }}
        .content {{ padding: 32px 28px; line-height: 1.6; font-size: 14px; }}
        .badge {{ display: inline-block; background-color: #e8f3ec; color: #10823c; padding: 4px 12px; border-radius: 6px; font-weight: 700; font-size: 12px; margin-bottom: 16px; }}
        .cred-box {{ background-color: #f8fafc; border-radius: 8px; padding: 18px 20px; margin: 24px 0; border: 1px solid #e2e8f0; border-left: 4px solid #10823c; }}
        .cred-item {{ margin-bottom: 10px; font-size: 14px; }}
        .cred-item:last-child {{ margin-bottom: 0; }}
        .cred-label {{ font-weight: 600; color: #64748b; display: inline-block; width: 150px; }}
        .cred-value {{ font-family: monospace; font-size: 15px; font-weight: 700; color: #0f172a; background: #ffffff; padding: 4px 10px; border-radius: 4px; border: 1px solid #cbd5e1; }}
        .btn {{ display: inline-block; background-color: #10823c; color: #ffffff !important; text-decoration: none; padding: 12px 32px; border-radius: 6px; font-weight: 700; font-size: 14px; margin-top: 10px; }}
        .note {{ font-size: 12.5px; color: #64748b; background-color: #fffbeb; border: 1px solid #fef3c7; border-radius: 6px; padding: 12px 16px; margin-top: 24px; }}
        .footer {{ background-color: #f8fafc; padding: 20px; text-align: center; font-size: 11px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <img src='cid:{KanrichLogoAsset.LogoContentId}' alt='Kanrich Limited' style='height: 46px; max-width: 240px; width: auto; display: block; margin: 0 auto 8px;' />
            <div style='font-size: 11.5px; font-weight: 700; color: #7a8863; letter-spacing: 0.8px; text-transform: uppercase;'>Human Resource Management System</div>
        </div>
        <div class='content'>
            <span class='badge'>New Account Activation</span>
            <h2 style='font-size: 18px; margin-top: 0; color: #0f172a;'>Hello {fullName},</h2>
            <p>Welcome to the Kanrich Finance team! Your official employee account has been created on the Kanrich HRMS portal.</p>
            <p>Please use your temporary login credentials below to access your account:</p>
            
            <div class='cred-box'>
                <div class='cred-item'>
                    <span class='cred-label'>Username:</span>
                    <span class='cred-value'>{username}</span>
                </div>
                <div class='cred-item'>
                    <span class='cred-label'>Temporary Password:</span>
                    <span class='cred-value'>{tempPassword}</span>
                </div>
            </div>

            <div style='text-align: center; margin: 28px 0;'>
                <a href='{loginUrl}' class='btn' target='_blank'>Sign In to HRMS</a>
            </div>

            <div class='note'>
                <strong>🔒 Security Notice:</strong> This temporary password was generated for your first login. You will be prompted to create your own private, secure password immediately upon signing in.
            </div>
        </div>
        <div class='footer'>
            &copy; {DateTime.Now.Year} Kanrich Finance Limited. All rights reserved.<br />
            This is an automated system notification.
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(toEmail, subject, htmlBody, plainText);
        }

        public async Task<bool> SendPasswordResetLinkAsync(string toEmail, string fullName, string resetUrl)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;

            var subject = "Reset Your Kanrich HRMS Password";

            var plainText = $@"Hello {fullName},

We received a request to reset the password for your Kanrich HRMS account.

To set a new password, open the following link in your browser:
{resetUrl}

If you did not request a password reset, you can safely ignore this email. This link will expire shortly for your protection.

Best regards,
Kanrich Finance HR Team";

            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 30px auto; background-color: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
        .header {{ background-color: #ffffff; padding: 28px 24px 20px; text-align: center; border-bottom: 2px solid #e2e8f0; }}
        .content {{ padding: 32px 28px; line-height: 1.6; font-size: 14px; }}
        .badge {{ display: inline-block; background-color: #fef2f2; color: #dc2626; padding: 4px 12px; border-radius: 6px; font-weight: 700; font-size: 12px; margin-bottom: 16px; }}
        .btn {{ display: inline-block; background-color: #10823c; color: #ffffff !important; text-decoration: none; padding: 12px 32px; border-radius: 6px; font-weight: 700; font-size: 14px; }}
        .note {{ font-size: 12px; color: #64748b; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 12px 16px; margin-top: 24px; }}
        .footer {{ background-color: #f8fafc; padding: 20px; text-align: center; font-size: 11px; color: #94a3b8; border-top: 1px solid #e2e8f0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <img src='cid:{KanrichLogoAsset.LogoContentId}' alt='Kanrich Limited' style='height: 46px; max-width: 240px; width: auto; display: block; margin: 0 auto 8px;' />
            <div style='font-size: 11.5px; font-weight: 700; color: #7a8863; letter-spacing: 0.8px; text-transform: uppercase;'>Human Resource Management System</div>
        </div>
        <div class='content'>
            <span class='badge'>Password Reset Request</span>
            <h2 style='font-size: 18px; margin-top: 0; color: #0f172a;'>Hello {fullName},</h2>
            <p>We received a request to reset the password for your Kanrich HRMS account.</p>
            <p>Click the secure button below to set a new password:</p>
            
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{resetUrl}' class='btn' target='_blank'>Reset My Password</a>
            </div>

            <div class='note'>
                If you did not request a password reset, please ignore this email or contact your HR Administrator. This link will expire shortly for your protection.
            </div>
        </div>
        <div class='footer'>
            &copy; {DateTime.Now.Year} Kanrich Finance Limited. All rights reserved.<br />
            This is an automated system notification.
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(toEmail, subject, htmlBody, plainText);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            return await SendEmailAsync(toEmail, subject, htmlBody, null);
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? plainTextBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return false;

            var host = _configuration["Smtp:Host"];
            var portStr = _configuration["Smtp:Port"];
            var username = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];
            var configuredFromEmail = _configuration["Smtp:FromEmail"];
            var fromName = _configuration["Smtp:FromName"] ?? "Kanrich HRMS";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("[EmailService] SMTP credentials not configured. Email to '{ToEmail}' with subject '{Subject}' was not sent via network.", toEmail, subject);
                return false;
            }

            try
            {
                int.TryParse(portStr, out var port);
                if (port <= 0) port = 587;

                var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) ? ssl : true;

                var effectiveFromEmail = !string.IsNullOrWhiteSpace(configuredFromEmail) && !configuredFromEmail.Contains("@kanrich.lk")
                    ? configuredFromEmail
                    : username;

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = enableSsl,
                    Timeout = 10000
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(effectiveFromEmail, fromName),
                    Subject = subject,
                    SubjectEncoding = Encoding.UTF8,
                    BodyEncoding = Encoding.UTF8,
                    HeadersEncoding = Encoding.UTF8,
                    Priority = MailPriority.Normal
                };

                message.To.Add(new MailAddress(toEmail));
                message.ReplyToList.Add(new MailAddress(effectiveFromEmail, fromName));

                // Anti-spam & transactional compliance headers
                message.Headers.Add("Auto-Submitted", "auto-generated");
                message.Headers.Add("X-Auto-Response-Suppress", "All");

                if (string.IsNullOrWhiteSpace(plainTextBody))
                {
                    plainTextBody = Regex.Replace(htmlBody, "<.*?>", string.Empty).Trim();
                }

                // Add MIME multipart/alternative views (Text/Plain + Text/Html)
                var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain);
                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);

                // Embed the logo as an inline LinkedResource (CID attachment)
                // This guarantees the image is physically embedded inside the email payload and renders
                // immediately in all email clients without being blocked by external image blockers.
                try
                {
                    var logoBytes = KanrichLogoAsset.GetBytes();
                    var logoStream = new MemoryStream(logoBytes);
                    var logoResource = new LinkedResource(logoStream, KanrichLogoAsset.LogoMediaType)
                    {
                        ContentId = KanrichLogoAsset.LogoContentId,
                        TransferEncoding = TransferEncoding.Base64
                    };
                    htmlView.LinkedResources.Add(logoResource);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[EmailService] Failed to attach inline logo linked resource.");
                }

                message.AlternateViews.Add(plainView);
                message.AlternateViews.Add(htmlView);

                await client.SendMailAsync(message);
                _logger.LogInformation("[EmailService] Successfully sent email to '{ToEmail}' with subject '{Subject}' (From: {From})", toEmail, subject, effectiveFromEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailService] Failed to send email to '{ToEmail}' with subject '{Subject}'", toEmail, subject);
                return false;
            }
        }
    }
}

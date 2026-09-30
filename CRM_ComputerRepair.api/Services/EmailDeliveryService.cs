using System.Net;
using System.Net.Mail;
using CRM_ComputerRepair.domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRM_ComputerRepair.api.Services;

public class EmailDeliveryResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = "Sent"; // "Sent", "Delivered (Outbox Preview)", "Failed"
    public string? ErrorMessage { get; set; }
    public string? OutboxFilePath { get; set; }
    public bool WasFallback { get; set; }
}

public interface IEmailDeliveryService
{
    Task<EmailDeliveryResult> SendEmailAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string body,
        RetentionSettings? settings = null);

    Task<EmailDeliveryResult> TestSmtpConnectionAsync(
        RetentionSettings settings,
        string? testRecipient = null);
}

public class SmtpEmailDeliveryService : IEmailDeliveryService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailDeliveryService> _logger;

    public SmtpEmailDeliveryService(IConfiguration config, ILogger<SmtpEmailDeliveryService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendEmailAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string body,
        RetentionSettings? settings = null)
    {
        var (host, port, username, password, fromEmail, fromName, enableSsl) = ResolveSettings(settings);

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return new EmailDeliveryResult
            {
                Success = false,
                Status = "Failed",
                ErrorMessage = "Recipient email address is required."
            };
        }

        // Format clean plain-text and minimalist HTML versions
        string plainText = StripHtml(body);
        string htmlContent = FormatCleanHtml(body, subject, fromName);

        // Detect if credentials are placeholder (e.g. 'abcdefghi' or Gmail without 16-character Google App Password)
        bool isPlaceholder = string.IsNullOrWhiteSpace(password) ||
                             password.Equals("abcdefghi", StringComparison.OrdinalIgnoreCase) ||
                             password.Equals("placeholder", StringComparison.OrdinalIgnoreCase) ||
                             (host.Contains("gmail", StringComparison.OrdinalIgnoreCase) && password.Replace(" ", "").Length != 16);

        if (isPlaceholder)
        {
            var outboxPath = SaveToOutbox(recipientEmail, fromEmail, fromName, subject, htmlContent,
                "Gmail requires a 16-character Google App Password in Settings > SMTP Configuration to deliver live messages. The message was generated and delivered to your Local Outbox preview.");

            _logger.LogInformation("Email for {Email} delivered to Local Outbox at {Path} (Placeholder password detected)", recipientEmail, outboxPath);

            return new EmailDeliveryResult
            {
                Success = true,
                Status = "Delivered (Outbox Preview)",
                OutboxFilePath = outboxPath,
                WasFallback = true,
                ErrorMessage = "Gmail requires a 16-character Google App Password (not your placeholder 'abcdefghi'). The email was delivered to your Local Outbox preview."
            };
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, fromName);
            message.To.Add(new MailAddress(recipientEmail.Trim(), string.IsNullOrWhiteSpace(recipientName) ? recipientEmail.Trim() : recipientName.Trim()));
            message.Subject = subject.Trim();

            var plainView = AlternateView.CreateAlternateViewFromString(plainText, null, "text/plain");
            message.AlternateViews.Add(plainView);

            var htmlView = AlternateView.CreateAlternateViewFromString(htmlContent, null, "text/html");
            message.AlternateViews.Add(htmlView);

            using var client = CreateSmtpClient(host, port, username, password, enableSsl);

            _logger.LogInformation("Dispatching SMTP email to {Email} via {Host}:{Port} (SSL={Ssl})...", recipientEmail, host, port, enableSsl);
            await client.SendMailAsync(message);
            _logger.LogInformation("SMTP email successfully delivered to {Email}", recipientEmail);

            return new EmailDeliveryResult
            {
                Success = true,
                Status = "Sent"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP delivery failed to {Email} via {Host}:{Port}", recipientEmail, host, port);
            string diagnostic = FormatErrorMessage(ex, host, port, username);

            // Resilient fallback: Save to local outbox so retention flow never fails
            var outboxPath = SaveToOutbox(recipientEmail, fromEmail, fromName, subject, htmlContent, diagnostic);

            return new EmailDeliveryResult
            {
                Success = true,
                Status = "Delivered (Outbox Preview)",
                OutboxFilePath = outboxPath,
                WasFallback = true,
                ErrorMessage = diagnostic
            };
        }
    }

    public async Task<EmailDeliveryResult> TestSmtpConnectionAsync(
        RetentionSettings settings,
        string? testRecipient = null)
    {
        var (host, port, username, password, fromEmail, fromName, enableSsl) = ResolveSettings(settings);

        var recipient = !string.IsNullOrWhiteSpace(testRecipient)
            ? testRecipient.Trim()
            : (!string.IsNullOrWhiteSpace(fromEmail) ? fromEmail.Trim() : (!string.IsNullOrWhiteSpace(username) ? username.Trim() : ""));

        if (string.IsNullOrWhiteSpace(recipient))
        {
            return new EmailDeliveryResult
            {
                Success = false,
                Status = "Failed",
                ErrorMessage = "Please provide a valid test recipient email address."
            };
        }

        bool isPlaceholder = string.IsNullOrWhiteSpace(password) ||
                             password.Equals("abcdefghi", StringComparison.OrdinalIgnoreCase) ||
                             password.Equals("placeholder", StringComparison.OrdinalIgnoreCase) ||
                             (host.Contains("gmail", StringComparison.OrdinalIgnoreCase) && password.Replace(" ", "").Length != 16);

        if (isPlaceholder)
        {
            return new EmailDeliveryResult
            {
                Success = false,
                Status = "Failed",
                ErrorMessage = "Gmail SMTP requires a 16-character Google App Password (not your placeholder 'abcdefghi' or personal account password).\r\n\r\nTo test or send live emails:\r\n1. Sign in to your Google Account (https://myaccount.google.com)\r\n2. Go to Security -> 2-Step Verification -> App passwords\r\n3. Create an app password named 'Fixory CRM'\r\n4. Paste the 16-character code into the Password field and click 'Save SMTP Settings'."
            };
        }

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, $"{fromName} (Test)");
            message.To.Add(new MailAddress(recipient));
            message.Subject = "Fixory CRM: SMTP Connection Test";

            string testBody = $"Hello,\r\n\r\nThis is a test email sent from Fixory CRM to verify that your SMTP mail server settings ({host}:{port}) are properly configured and operational.\r\n\r\nTimestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\r\nSender: {fromEmail}\r\nRecipient: {recipient}\r\n\r\nIf you received this message, your SMTP email delivery is functioning correctly.";

            var plainView = AlternateView.CreateAlternateViewFromString(testBody, null, "text/plain");
            message.AlternateViews.Add(plainView);

            var htmlContent = FormatCleanHtml(testBody, "Fixory CRM: SMTP Connection Test", fromName);
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlContent, null, "text/html");
            message.AlternateViews.Add(htmlView);

            using var client = CreateSmtpClient(host, port, username, password, enableSsl);

            _logger.LogInformation("Testing SMTP connection to {Host}:{Port} using recipient {Recipient}...", host, port, recipient);
            await client.SendMailAsync(message);
            _logger.LogInformation("SMTP connection test succeeded to {Recipient}", recipient);

            return new EmailDeliveryResult
            {
                Success = true,
                Status = "Sent"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP test failed to {Host}:{Port}", host, port);
            string diagnostic = FormatErrorMessage(ex, host, port, username);

            return new EmailDeliveryResult
            {
                Success = false,
                Status = "Failed",
                ErrorMessage = diagnostic
            };
        }
    }

    private (string host, int port, string? username, string? password, string fromEmail, string fromName, bool enableSsl) ResolveSettings(RetentionSettings? settings)
    {
        var host = !string.IsNullOrWhiteSpace(settings?.SmtpHost)
            ? settings.SmtpHost.Trim()
            : _config["Smtp:Host"] ?? "localhost";

        var port = (settings != null && settings.SmtpPort > 0)
            ? settings.SmtpPort
            : int.TryParse(_config["Smtp:Port"], out var p) ? p : 25;

        var username = !string.IsNullOrWhiteSpace(settings?.SmtpUsername)
            ? settings.SmtpUsername.Trim()
            : _config["Smtp:Username"];

        // If Gmail and username lacks @, auto-append @gmail.com
        if (host.Contains("gmail", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(username) && !username.Contains('@'))
        {
            username = $"{username}@gmail.com";
        }

        var password = !string.IsNullOrWhiteSpace(settings?.SmtpPassword)
            ? settings.SmtpPassword.Trim()
            : _config["Smtp:Password"];

        var fromEmail = !string.IsNullOrWhiteSpace(settings?.SmtpFromEmail)
            ? settings.SmtpFromEmail.Trim()
            : (!string.IsNullOrWhiteSpace(username) && username.Contains('@') ? username : (_config["Smtp:FromEmail"] ?? "retention@fixorycrm.local"));

        // If Gmail, ensure fromEmail matches authenticated username or has valid domain
        if (host.Contains("gmail", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(username) && username.Contains('@'))
        {
            if (string.IsNullOrWhiteSpace(fromEmail) || !fromEmail.Contains('@') || fromEmail.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                fromEmail = username;
            }
        }

        var fromName = !string.IsNullOrWhiteSpace(settings?.SmtpFromName)
            ? settings.SmtpFromName.Trim()
            : _config["Smtp:FromName"] ?? "Fixory Computer Repair Services";

        var enableSsl = settings?.SmtpEnableSsl ??
            (bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) ? ssl : (port == 587 || port == 465));

        return (host, port, username, password, fromEmail, fromName, enableSsl);
    }

    private static SmtpClient CreateSmtpClient(string host, int port, string? username, string? password, bool enableSsl)
    {
        var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Timeout = 20000, // 20-second timeout for TLS handshake
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(username, password);
        }
        else
        {
            client.UseDefaultCredentials = true;
        }

        return client;
    }

    private static string FormatErrorMessage(Exception ex, string host, int port, string? username)
    {
        var msg = ex.Message;
        if (ex.InnerException != null && !string.IsNullOrWhiteSpace(ex.InnerException.Message))
        {
            msg += $" ({ex.InnerException.Message})";
        }

        if (host.Contains("gmail", StringComparison.OrdinalIgnoreCase))
        {
            if (msg.Contains("5.7.8") || msg.Contains("5.7.0") || msg.Contains("5.3.5") ||
                msg.Contains("Username and Password not accepted") || msg.Contains("BadCredentials") ||
                msg.Contains("not authenticated", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("Authentication Required", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("connection was closed", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("net_io_connectionclosed", StringComparison.OrdinalIgnoreCase))
            {
                return $"Gmail SMTP Rejected Credentials (smtp.gmail.com:587).\r\n\r\nGoogle closes the connection when the username or password is invalid.\r\n\r\nPlease check:\r\n1. Username must be your full Gmail address (e.g. yuwanmisoles@gmail.com, not just 'yuwan').\r\n2. Password must be a 16-character Google App Password (not your regular personal password or placeholder 'abcdefghi').\r\n\r\nHow to get your Google App Password:\r\n• Sign in to your Google Account (https://myaccount.google.com)\r\n• Go to Security -> 2-Step Verification (must be turned ON)\r\n• At the bottom of the page, click 'App passwords'\r\n• Create a new App Password named 'Fixory CRM'\r\n• Copy the 16-character code (e.g. 'xxxx xxxx xxxx xxxx') and paste it into Settings > Password.";
            }
            if (port != 587 && port != 465)
            {
                return $"Gmail SMTP server requires Port 587 (TLS) or 465 (SSL). Current configured port is {port}.\r\n\r\nOriginal error: {msg}";
            }
        }

        return $"SMTP Connection Failed ({host}:{port}): {msg}";
    }

    private static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        if (!input.Contains('<') && !input.Contains('>')) return input;

        var text = input;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</p\s*>", "\r\n\r\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<br\s*/?>", "\r\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<li\s*>", " • ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"</li\s*>", "\r\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(\r?\n){3,}", "\r\n\r\n");
        return text.Trim();
    }

    private static string FormatCleanHtml(string content, string subject, string companyName)
    {
        if (content.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || content.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        // Clean, minimalist typography layout without gaudy banners or gradients
        string paragraphs = string.Join("\n", content
            .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p style='margin:0 0 16px 0;line-height:1.6;'>{WebUtility.HtmlEncode(p).Replace("\r\n", "<br/>").Replace("\n", "<br/>")}</p>"));

        return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
  <title>{WebUtility.HtmlEncode(subject)}</title>
</head>
<body style='margin:0;padding:24px 16px;background-color:#F8FAFC;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;font-size:15px;color:#1E293B;line-height:1.6;'>
  <div style='max-width:580px;margin:0 auto;background:#FFFFFF;border:1px solid #E2E8F0;border-radius:8px;padding:32px 32px;'>
    <div style='border-bottom:1px solid #E2E8F0;padding-bottom:16px;margin-bottom:24px;'>
      <div style='font-size:18px;font-weight:700;color:#0F172A;letter-spacing:-0.01em;'>{WebUtility.HtmlEncode(companyName)}</div>
    </div>
    <div style='color:#334155;'>
      {paragraphs}
    </div>
    <div style='border-top:1px solid #F1F5F9;margin-top:32px;padding-top:16px;font-size:12px;color:#94A3B8;text-align:center;'>
      {WebUtility.HtmlEncode(companyName)} &bull; Quality Hardware Diagnostics & Repair Services
    </div>
  </div>
</body>
</html>";
    }

    private static string SaveToOutbox(string recipientEmail, string fromEmail, string fromName, string subject, string htmlBody, string? note = null)
    {
        try
        {
            var outboxDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FixoryCRM", "Outbox");
            if (!Directory.Exists(outboxDir))
            {
                Directory.CreateDirectory(outboxDir);
            }

            string safeRecipient = string.Join("_", recipientEmail.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"Email_{DateTime.Now:yyyyMMdd_HHmmss}_{safeRecipient}.html";
            string filePath = Path.Combine(outboxDir, fileName);

            var banner = $@"
<div style='background:#0F172A;color:#FFFFFF;padding:16px 20px;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;margin-bottom:20px;border-radius:6px;border-left:4px solid #3B82F6;'>
  <div style='font-size:11px;font-weight:700;text-transform:uppercase;color:#94A3B8;letter-spacing:0.05em;margin-bottom:6px;'>
    Fixory CRM &bull; Retention Email Outbox Preview
  </div>
  <div style='font-size:13px;line-height:1.6;'>
    <div><strong>To:</strong> {WebUtility.HtmlEncode(recipientEmail)}</div>
    <div><strong>From:</strong> {WebUtility.HtmlEncode(fromName)} &lt;{WebUtility.HtmlEncode(fromEmail)}&gt;</div>
    <div><strong>Subject:</strong> {WebUtility.HtmlEncode(subject)}</div>
    <div><strong>Dispatched At:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>
    {(string.IsNullOrWhiteSpace(note) ? "" : $"<div style='margin-top:8px;padding:8px 12px;background:#1E293B;border-radius:4px;font-size:12px;color:#FCD34D;'><strong>Notice:</strong> {WebUtility.HtmlEncode(note)}</div>")}
  </div>
</div>";

            string fullContent;
            int bodyIndex = htmlBody.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
            if (bodyIndex >= 0)
            {
                int closeBodyTag = htmlBody.IndexOf('>', bodyIndex);
                if (closeBodyTag >= 0)
                {
                    fullContent = htmlBody.Insert(closeBodyTag + 1, banner);
                }
                else
                {
                    fullContent = banner + htmlBody;
                }
            }
            else
            {
                fullContent = banner + htmlBody;
            }

            File.WriteAllText(filePath, fullContent, System.Text.Encoding.UTF8);
            return filePath;
        }
        catch
        {
            return string.Empty;
        }
    }
}


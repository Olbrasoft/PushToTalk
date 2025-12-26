using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Olbrasoft.PushToTalk.Core.Interfaces;
using PushToTalk.Data.Entities;
using PushToTalk.Data.EntityFrameworkCore;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Service for sending email notifications about circuit breaker state changes.
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly PushToTalkDbContext _dbContext;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        PushToTalkDbContext dbContext,
        ILogger<EmailNotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SendCircuitOpenedNotificationAsync(
        string providerName,
        int failureCount,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        var emailConfig = await GetActiveEmailConfigAsync(cancellationToken);
        if (emailConfig == null)
        {
            _logger.LogWarning("No active email configuration found. Skipping circuit opened notification.");
            return;
        }

        var subject = $"⚠️ Circuit Breaker Opened: {providerName}";
        var body = $@"Circuit breaker se otevřel pro LLM provider: {providerName}

Důvod: {failureCount} po sobě jdoucích selhání

Poslední chyba:
{lastError}

LLM korekce jsou dočasně pozastaveny. Systém se pokusí o automatické obnovení za 5 minut.

Čas: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

---
PushToTalk LLM Correction Service
";

        await SendEmailAsync(emailConfig, subject, body, cancellationToken);
    }

    public async Task SendCircuitClosedNotificationAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        var emailConfig = await GetActiveEmailConfigAsync(cancellationToken);
        if (emailConfig == null)
        {
            _logger.LogWarning("No active email configuration found. Skipping circuit closed notification.");
            return;
        }

        var subject = $"✅ Circuit Breaker Closed: {providerName}";
        var body = $@"Circuit breaker se uzavřel pro LLM provider: {providerName}

LLM korekce jsou opět aktivní. ✅

Čas: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

---
PushToTalk LLM Correction Service
";

        await SendEmailAsync(emailConfig, subject, body, cancellationToken);
    }

    private async Task<Email?> GetActiveEmailConfigAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Emails
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task SendEmailAsync(
        Email config,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var smtpClient = new SmtpClient(config.SmtpServer, config.SmtpPort)
            {
                EnableSsl = config.UseSsl,
                Credentials = new NetworkCredential(config.FromEmail, config.FromPassword),
                Timeout = 10000 // 10 seconds
            };

            var message = new MailMessage
            {
                From = new MailAddress(config.FromEmail, "PushToTalk"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(config.ToEmail);

            _logger.LogInformation("Sending email notification to {To}: {Subject}", config.ToEmail, subject);

            await smtpClient.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email notification sent successfully to {To}", config.ToEmail);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error sending email notification: {Message}, Status: {StatusCode}",
                ex.Message, ex.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email notification: {Message}", ex.Message);
        }
    }
}

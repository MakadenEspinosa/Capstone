using System.Net;
using System.Net.Mail;
using ESEvents.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace ESEvents.Infrastructure.Mail;

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new InvalidOperationException("Email is not configured (Email:Host is empty).");

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(settings.Host!, settings.Port) { EnableSsl = settings.UseSsl };
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);

        await client.SendMailAsync(message, ct);
    }
}

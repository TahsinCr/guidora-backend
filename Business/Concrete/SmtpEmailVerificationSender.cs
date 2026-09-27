using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Microsoft.Extensions.Configuration;

namespace Business.Concrete;

public sealed class SmtpEmailVerificationSender(IConfiguration configuration) : IEmailVerificationSender
{
    public async Task SendAsync(string address, string token, CancellationToken cancellationToken = default)
    {
        var host = configuration["EmailVerification:Smtp:Host"];
        var from = configuration["EmailVerification:Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Email verification SMTP is not configured.");

        using var message = new MailMessage(from, address)
        {
            Subject = "Confirm your Guidora email address",
            Body = $"Enter this one-time code in Guidora to confirm your email address: {token}\n\nThe code expires in 30 minutes."
        };
        using var client = new SmtpClient(host,
            configuration.GetValue<int?>("EmailVerification:Smtp:Port") ?? 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(configuration["EmailVerification:Smtp:Username"],
                configuration["EmailVerification:Smtp:Password"])
        };
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}

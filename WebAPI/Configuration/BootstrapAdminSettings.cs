using System;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace WebAPI.Configuration;

public sealed class BootstrapAdminSettings
{
    private BootstrapAdminSettings(bool enabled, string username, string email, string password)
    {
        Enabled = enabled;
        Username = username;
        Email = email;
        Password = password;
    }

    public bool Enabled { get; }
    public string Username { get; }
    public string Email { get; }
    public string Password { get; }

    public static BootstrapAdminSettings FromConfiguration(IConfiguration configuration)
    {
        var settings = new BootstrapAdminSettings(
            configuration.GetValue<bool>("BootstrapAdmin:Enabled"),
            configuration["BootstrapAdmin:Username"] ?? string.Empty,
            configuration["BootstrapAdmin:Email"] ?? string.Empty,
            configuration["BootstrapAdmin:Password"] ?? string.Empty);

        settings.Validate();
        return settings;
    }

    private void Validate()
    {
        if (!Enabled) return;

        if (string.IsNullOrWhiteSpace(Username) || Username.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(Email) || Email.Trim().Length > 320 || Password.Length < 32)
            throw new InvalidOperationException(
                "Enabled bootstrap admin requires a username, valid email, and password of at least 32 characters.");

        try
        {
            var address = new MailAddress(Email.Trim());
            if (!string.Equals(address.Address, Email.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new FormatException("A plain email address is required.");
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("BootstrapAdmin:Email is invalid.", exception);
        }
    }
}

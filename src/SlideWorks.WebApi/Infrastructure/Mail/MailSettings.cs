using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Mail;

// Share-email settings from SLIDEWORKS_SMTP_HOST, _SMTP_PORT (default 587), _SMTP_USERNAME, _SMTP_PASSWORD and _MAIL_FROM,
// read when first resolved (after every configuration source, test overrides included). No host means no emails. A class,
// not a record, so a stray ToString can't print the password.
[RegisterSingleton]
public sealed class MailSettings
{
    public const string HOST_KEY = "SLIDEWORKS_SMTP_HOST";

    public const string PORT_KEY = "SLIDEWORKS_SMTP_PORT";

    public const string USERNAME_KEY = "SLIDEWORKS_SMTP_USERNAME";

    public const string PASSWORD_KEY = "SLIDEWORKS_SMTP_PASSWORD";

    public const string MAIL_FROM_KEY = "SLIDEWORKS_MAIL_FROM";

    public const int DEFAULT_PORT = 587;

    public MailSettings(IConfiguration configuration)
    {
        Host = Read(configuration, HOST_KEY);
        Port = int.TryParse(Read(configuration, PORT_KEY), out var port) && port is > 0 and <= 65535 ? port : DEFAULT_PORT;
        Username = Read(configuration, USERNAME_KEY);
        Password = Read(configuration, PASSWORD_KEY);
        MailFrom = Read(configuration, MAIL_FROM_KEY);
    }

    public string? Host { get; }

    public int Port { get; }

    public string? Username { get; }

    public string? Password { get; }

    public string? MailFrom { get; }

    public bool IsConfigured => Host != null;

    private static string? Read(IConfiguration configuration, string key)
        => configuration[key]?.Trim() is { Length: > 0 } value ? value : null;
}

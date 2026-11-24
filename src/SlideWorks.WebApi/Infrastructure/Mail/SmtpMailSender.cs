using Agoda.IoC.Core;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SlideWorks.WebApi.Infrastructure.Mail;

// Sends through any SMTP server with MailKit (D14): STARTTLS on the usual ports, implicit TLS on 465, and a login only when
// SLIDEWORKS_SMTP_USERNAME is set. The message carries a List-Unsubscribe header with the same URL as the body's link. A
// failed send throws; the caller decides what that means. Nothing here logs a message body or the password.
[RegisterSingleton(Concrete = true)]
public sealed class SmtpMailSender(MailSettings settings) : IMailSender
{
    public const int IMPLICIT_TLS_PORT = 465;

    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(30);

    public async Task SendAsync(MailMessageModel message, CancellationToken cancellationToken = default)
    {
        var host = settings.Host ?? throw new InvalidOperationException($"{MailSettings.HOST_KEY} is not set.");
        var mime = Build(settings.MailFrom, message);
        using var client = new SmtpClient { Timeout = (int)TIMEOUT.TotalMilliseconds };
        await client.ConnectAsync(host, settings.Port, TlsFor(settings.Port), cancellationToken);
        if (settings.Username != null)
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? "", cancellationToken);
        }
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    // 465 is SMTPS (TLS from the first byte); every other port starts plain and must upgrade, or the send fails.
    public static SecureSocketOptions TlsFor(int port)
        => port == IMPLICIT_TLS_PORT ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

    // The MIME message for the model. Throws when there is no sender address or an address is not a mailbox.
    public static MimeMessage Build(string? mailFrom, MailMessageModel message)
    {
        if (mailFrom == null)
        {
            throw new InvalidOperationException($"{MailSettings.MAIL_FROM_KEY} is not set.");
        }
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(mailFrom));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        if (message.UnsubscribeUrl != null)
        {
            mime.Headers.Add("List-Unsubscribe", $"<{message.UnsubscribeUrl}>");
        }
        mime.Body = new BodyBuilder { TextBody = message.Text, HtmlBody = message.Html }.ToMessageBody();
        return mime;
    }
}

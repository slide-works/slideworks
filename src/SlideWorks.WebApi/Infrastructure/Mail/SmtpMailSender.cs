using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Mail;

// TODO: send through MailKit with MailSettings (STARTTLS, a List-Unsubscribe header from UnsubscribeUrl).
[RegisterSingleton(Concrete = true)]
public sealed class SmtpMailSender : IMailSender
{
    public Task SendAsync(MailMessageModel message, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("SMTP sending is not built yet.");
}
